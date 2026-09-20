using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Orders;

public sealed class OrderService(StoreDbContext db)
{
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

    public async Task<object> DetailAsync(Guid id, long? customer, CancellationToken ct)
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
        return new
        {
            fulfillmentWarehouseId = reservedWarehouse,
            id = order.PublicId,
            order.OrderNumber,
            order.Status,
            order.Currency,
            order.PlacedAt,
            order.PaidAt,
            order.CancelledAt,
            order.CompletedAt,
            customer = new
            {
                id = order.Customer.PublicId,
                name = order.Customer.LastName + " " + order.Customer.FirstName,
                order.Customer.Email,
            },
            totals = new
            {
                order.Subtotal,
                order.DiscountTotal,
                order.TaxTotal,
                order.ShippingTotal,
                order.GrandTotal,
                order.Currency,
            },
            items = order
                .OrderItems.OrderBy(i => i.Id)
                .Select(i => new
                {
                    id = i.PublicId,
                    i.Sku,
                    i.ProductName,
                    i.VariantName,
                    i.Quantity,
                    i.UnitPrice,
                    i.DiscountAmount,
                    i.TaxAmount,
                    i.LineTotal,
                    reviewStatus = i.ProductReview?.Status,
                }),
            addresses = order.OrderAddresses.Select(a => new
            {
                a.AddressType,
                a.RecipientName,
                a.PostalCode,
                a.CountryCode,
                a.Prefecture,
                a.City,
                a.AddressLine1,
                a.AddressLine2,
                a.Phone,
            }),
            history = order
                .OrderStatusHistories.OrderBy(h => h.CreatedAt)
                .ThenBy(h => h.Id)
                .Select(h => new
                {
                    h.FromStatus,
                    h.ToStatus,
                    h.Reason,
                    h.CreatedAt,
                }),
            payments = order
                .Payments.OrderBy(p => p.Id)
                .Select(p => new
                {
                    id = p.PublicId,
                    p.Provider,
                    p.Method,
                    p.Status,
                    p.Amount,
                    p.Currency,
                    p.CreatedAt,
                    p.CapturedAt,
                    refunds = p.Refunds.Select(r => new
                    {
                        id = r.PublicId,
                        r.Amount,
                        r.Reason,
                        r.Status,
                        r.CreatedAt,
                    }),
                }),
            shipments = order
                .Shipments.OrderBy(s => s.Id)
                .Select(s => new
                {
                    id = s.PublicId,
                    s.Carrier,
                    s.TrackingNumber,
                    s.Status,
                    s.ShippedAt,
                    s.DeliveredAt,
                    warehouse = s.Warehouse.Name,
                }),
        };
    }
}
