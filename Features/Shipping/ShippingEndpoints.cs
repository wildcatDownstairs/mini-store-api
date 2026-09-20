using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;
using MiniStore.Features.Orders;

namespace MiniStore.Features.Shipping;

public sealed record ShipRequest(Guid WarehouseId, string Carrier, string TrackingNumber);

public static class ShippingEndpoints
{
    public static void MapShipping(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminRead")
            .WithTags("发货与配送");
        admin.MapGet(
            "/shipments",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
            {
                q.Validate();
                var source = db.Shipments.AsNoTracking();
                if (q.Warehouse.HasValue)
                    source = source.Where(s => s.Warehouse.PublicId == q.Warehouse);
                if (q.Status != null)
                    source = source.Where(s => s.Status == q.Status);
                if (q.Q is { Length: > 0 })
                {
                    var pattern = "%" + q.Q.Trim() + "%";
                    source = source.Where(s =>
                        EF.Functions.ILike(s.Order.OrderNumber, pattern)
                        || s.TrackingNumber != null && EF.Functions.ILike(s.TrackingNumber, pattern)
                    );
                }
                return await source
                    .OrderByDescending(s => s.CreatedAt)
                    .ThenByDescending(s => s.Id)
                    .Select(s => new
                    {
                        id = s.PublicId,
                        orderId = s.Order.PublicId,
                        s.Order.OrderNumber,
                        s.Carrier,
                        s.TrackingNumber,
                        s.Status,
                        s.ShippedAt,
                        s.DeliveredAt,
                        warehouseId = s.Warehouse.PublicId,
                        warehouse = s.Warehouse.Name,
                    })
                    .PageAsync(q, ct);
            }
        );
        admin
            .MapPost(
                "/orders/{id:guid}/ship",
                async (
                    Guid id,
                    ShipRequest r,
                    StoreDbContext db,
                    OrderService orders,
                    CancellationToken ct
                ) =>
                {
                    Rules.Require(
                        r.Carrier is "Yamato" or "Sagawa" or "Japan Post",
                        "请选择支持的承运商。"
                    );
                    Rules.Require(
                        Regex.IsMatch(r.TrackingNumber ?? "", "^[A-Za-z0-9-]{8,30}$"),
                        "运单号须为 8～30 位字母、数字或连字符。"
                    );
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var order = await RowLocks.Order(db, id, null, ct);
                    if (order.Status != "processing" || order.PaidAt == null)
                        throw ApiError.Conflict("订单必须已付款并进入配货状态。");
                    var warehouse =
                        await db.Warehouses.SingleOrDefaultAsync(
                            w => w.PublicId == r.WarehouseId,
                            ct
                        ) ?? throw new ApiError(400, "warehouse", "仓库不存在。");
                    var reservedWarehouses = await db
                        .StockMovements.Where(m =>
                            m.OrderId == order.Id
                            && (m.MovementType == "reservation" || m.MovementType == "release")
                        )
                        .GroupBy(m => m.WarehouseId)
                        .Where(g => g.Sum(m => m.Quantity) > 0)
                        .Select(g => g.Key)
                        .ToListAsync(ct);
                    if (reservedWarehouses.Count != 1 || reservedWarehouses[0] != warehouse.Id)
                        throw ApiError.Conflict(
                            "发货仓必须与订单预占仓一致；当前 API 不支持拆仓发货。"
                        );
                    var existing = await db
                        .Shipments.Where(s => s.OrderId == order.Id)
                        .ToListAsync(ct);
                    if (
                        existing.Count > 1
                        || existing.Any(s => s.Status is not ("pending" or "ready"))
                    )
                        throw ApiError.Conflict("订单已有已发运物流，不能重复发货。");
                    await orders.ReleaseAsync(order, true, ct);
                    var now = DateTime.UtcNow;
                    var shipment =
                        existing.SingleOrDefault()
                        ?? new Shipment { OrderId = order.Id, CreatedAt = now };
                    shipment.WarehouseId = warehouse.Id;
                    shipment.Carrier = r.Carrier;
                    shipment.TrackingNumber = r.TrackingNumber;
                    shipment.Status = "shipped";
                    shipment.UpdatedAt = now;
                    shipment.ShippedAt = now;
                    if (existing.Count == 0)
                        db.Shipments.Add(shipment);
                    OrderService.Transition(order, "shipped", "仓库出库并交付承运商");
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Results.Ok(new { id = shipment.PublicId });
                }
            )
            .RequireAuthorization("AdminWrite");
        admin
            .MapPost(
                "/orders/{id:guid}/deliver",
                async (Guid id, StoreDbContext db, CancellationToken ct) =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var order = await RowLocks.Order(db, id, null, ct);
                    if (order.Status != "shipped")
                        throw ApiError.Conflict("只有已发货订单可以确认送达。");
                    var shipments = await db
                        .Shipments.Where(s => s.OrderId == order.Id)
                        .ToListAsync(ct);
                    if (
                        shipments.Count == 0
                        || shipments.Any(s => s.Status is not ("shipped" or "in_transit"))
                    )
                        throw ApiError.Conflict("物流状态不允许签收。");
                    foreach (var shipment in shipments)
                    {
                        shipment.Status = "delivered";
                        shipment.DeliveredAt = DateTime.UtcNow;
                        shipment.UpdatedAt = DateTime.UtcNow;
                    }
                    OrderService.Transition(order, "delivered", "运营人员确认配送完成");
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
    }
}
