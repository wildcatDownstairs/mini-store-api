using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Inventory;

namespace MiniStore.Features.Orders;

/// <summary>读取订单快照并协调状态与库存释放；写操作由调用方法明确管理事务。</summary>
public sealed class OrderService(StoreDbContext db)
{
    /// <summary>同步修改订单状态、时间和历史集合；不校验合法状态跳转，也不保存，调用方必须先校验并提交。</summary>
    public static void Transition(Order order, string to, string reason)
    {
        // 状态改变与历史记录一起保存，不能只改 orders.status。
        var now = DateTime.UtcNow;
        order.OrderStatusHistories.Add(
            new()
            {
                FromStatus = order.Status,
                ToStatus = to,
                Reason = reason,
                CreatedAt = now,
            }
        );
        order.Status = to;
        order.UpdatedAt = now;
        if (to == "paid")
            order.PaidAt = now;
        if (to == "cancelled")
            order.CancelledAt = now;
        if (to == "delivered")
            order.CompletedAt = now;
    }

    /// <summary>锁定订单后取消未付款订单并释放预占；已使用优惠券次数和核销记录保留。</summary>
    public async Task CancelAsync(Guid id, long? customer, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await RowLocks.Order(db, id, customer, ct);
        if (order.Status is not ("pending" or "confirmed") || order.PaidAt != null)
            throw ApiError.Conflict("只有未付款订单可以取消。");
        await ReleaseAsync(order, false, ct);
        Transition(order, "cancelled", "取消未付款订单；优惠核销保留，不返还次数");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>按订单流水追溯预占，不能释放其他订单的库存；sale=true 同时扣减实物库存。</summary>
    public async Task ReleaseAsync(Order order, bool sale, CancellationToken ct)
    {
        // 调用方必须已开启事务并锁定订单；本方法不独立提交，让取消或发货操作可以整体回滚。
        var reservations = await db
            .StockMovements.Where(m =>
                m.OrderId == order.Id
                && (m.MovementType == "reservation" || m.MovementType == "release")
            )
            .GroupBy(m => new { m.WarehouseId, m.VariantId })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.VariantId,
                Quantity = g.Sum(m => m.Quantity),
            })
            .Where(g => g.Quantity > 0)
            .OrderBy(g => g.WarehouseId)
            .ThenBy(g => g.VariantId)
            .ToListAsync(ct);
        if (sale)
        {
            var needed = await db
                .OrderItems.Where(i => i.OrderId == order.Id)
                .GroupBy(i => i.VariantId)
                .Select(g => new { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToListAsync(ct);
            if (
                needed.Any(n =>
                    reservations.Where(r => r.VariantId == n.VariantId).Sum(r => r.Quantity)
                    != n.Quantity
                )
            )
                throw ApiError.Conflict("预占库存与订单不一致，不能发货。");
        }
        foreach (var reservation in reservations)
        {
            var stock = await db
                .Stocks.FromSql(
                    $"SELECT * FROM inventory.stocks WHERE warehouse_id={reservation.WarehouseId} AND variant_id={reservation.VariantId} FOR UPDATE"
                )
                .SingleAsync(ct);
            if (stock.QuantityReserved < reservation.Quantity)
                throw ApiError.Conflict("预占库存已变化。");
            stock.QuantityReserved -= reservation.Quantity;
            if (sale)
                stock.QuantityOnHand -= reservation.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
            db.StockMovements.Add(Movement(order.Id, stock, "release", -reservation.Quantity));
            if (sale)
                db.StockMovements.Add(Movement(order.Id, stock, "sale", -reservation.Quantity));
        }
    }

    /// <summary>创建订单关联的库存流水对象；仅构造对象，不单独写入数据库。</summary>
    private static StockMovement Movement(long orderId, Stock stock, string type, int quantity) =>
        new()
        {
            WarehouseId = stock.WarehouseId,
            VariantId = stock.VariantId,
            MovementType = type,
            Quantity = quantity,
            ReferenceType = "order",
            ReferenceId = orderId,
            Note = "API 订单库存操作",
            CreatedAt = DateTime.UtcNow,
        };

    /// <summary>读取订单成交快照及付款、退款和物流；customer 有值时限制归属，null 仅供已授权后台调用。</summary>
    public async Task<OrderDetailDto> DetailAsync(Guid id, long? customer, CancellationToken ct)
    {
        var order =
            await db
                .Orders.AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.ProductReview)
                .Include(o => o.OrderAddresses)
                .Include(o => o.OrderStatusHistories)
                .Include(o => o.Payments)
                    .ThenInclude(p => p.Refunds)
                .Include(o => o.Shipments)
                    .ThenInclude(s => s.Warehouse)
                .SingleOrDefaultAsync(
                    o => o.PublicId == id && (!customer.HasValue || o.CustomerId == customer),
                    ct
                )
            ?? throw ApiError.NotFound();
        var reservedWarehouse = await db
            .StockMovements.Where(m =>
                m.OrderId == order.Id
                && (m.MovementType == "reservation" || m.MovementType == "release")
            )
            .GroupBy(m => m.Stock.Warehouse.PublicId)
            .Where(g => g.Sum(m => m.Quantity) > 0)
            .Select(g => (Guid?)g.Key)
            .FirstOrDefaultAsync(ct);
        return new OrderDetailDto(
            reservedWarehouse,
            order.PublicId,
            order.OrderNumber,
            order.Status,
            order.Currency,
            order.PlacedAt,
            order.PaidAt,
            order.CancelledAt,
            order.CompletedAt,
            new OrderCustomerDto(
                order.Customer.PublicId,
                order.Customer.LastName + " " + order.Customer.FirstName,
                order.Customer.Email
            ),
            new OrderTotalsDto(
                order.Subtotal,
                order.DiscountTotal,
                order.TaxTotal,
                order.ShippingTotal,
                order.GrandTotal,
                order.Currency
            ),
            order
                .OrderItems.OrderBy(i => i.Id)
                .Select(i => new OrderItemDto(
                    i.PublicId,
                    i.Sku,
                    i.ProductName,
                    i.VariantName,
                    i.Quantity,
                    i.UnitPrice,
                    i.DiscountAmount,
                    i.TaxAmount,
                    i.LineTotal,
                    i.ProductReview?.Status
                )),
            order.OrderAddresses.Select(a => new OrderAddressDto(
                a.AddressType,
                a.RecipientName,
                a.PostalCode,
                a.CountryCode,
                a.Prefecture,
                a.City,
                a.AddressLine1,
                a.AddressLine2,
                a.Phone
            )),
            order
                .OrderStatusHistories.OrderBy(h => h.CreatedAt)
                .ThenBy(h => h.Id)
                .Select(h => new OrderHistoryDto(h.FromStatus, h.ToStatus, h.Reason, h.CreatedAt)),
            order
                .Payments.OrderBy(p => p.Id)
                .Select(p => new OrderPaymentDto(
                    p.PublicId,
                    p.Provider,
                    p.Method,
                    p.Status,
                    p.Amount,
                    p.Currency,
                    p.CreatedAt,
                    p.CapturedAt,
                    p.Refunds.Select(r => new OrderRefundDto(
                        r.PublicId,
                        r.Amount,
                        r.Reason,
                        r.Status,
                        r.CreatedAt
                    ))
                )),
            order
                .Shipments.OrderBy(s => s.Id)
                .Select(s => new OrderShipmentDto(
                    s.PublicId,
                    s.Carrier,
                    s.TrackingNumber,
                    s.Status,
                    s.ShippedAt,
                    s.DeliveredAt,
                    s.Warehouse.Name
                ))
        );
    }

    /// <summary>锁定订单并要求当前状态等于 from，随后统一记录状态历史并提交。</summary>
    public async Task ChangeAsync(Guid id, string from, string to, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await RowLocks.Order(db, id, null, ct);
        if (order.Status != from)
            throw ApiError.Conflict("订单状态不允许此操作。");
        OrderService.Transition(order, to, "运营人员处理订单");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>分页查询订单；客户入口限制本人，后台入口可跨客户，二者复用同一投影。</summary>
    public async Task<PageResult<OrderSummaryDto>> ListAsync(
        ListQuery q,
        long? customer,
        CancellationToken ct
    )
    {
        q.Validate();
        var orders = db.Orders.AsNoTracking();
        if (customer.HasValue)
            orders = orders.Where(o => o.CustomerId == customer);
        if (q.Status is { Length: > 0 })
            orders = orders.Where(o => o.Status == q.Status);
        if (q.Q is { Length: > 0 })
        {
            var pattern = "%" + q.Q.Trim() + "%";
            orders = orders.Where(o =>
                EF.Functions.ILike(o.OrderNumber, pattern)
                || EF.Functions.ILike(o.Customer.Email, pattern)
            );
        }
        return await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Select(o => new OrderSummaryDto(
                o.PublicId,
                o.OrderNumber,
                o.Status,
                o.Currency,
                o.GrandTotal,
                o.PlacedAt,
                o.PaidAt,
                o.Customer.LastName + " " + o.Customer.FirstName,
                o.OrderItems.Sum(i => i.Quantity),
                o.OrderItems.OrderBy(i => i.Id).Select(i => i.ProductName).FirstOrDefault(),
                o.Payments.OrderByDescending(p => p.Id).Select(p => p.Status).FirstOrDefault(),
                o.Shipments.OrderByDescending(s => s.Id).Select(s => s.Status).FirstOrDefault()
            ))
            .PageAsync(q, ct);
    }
}
