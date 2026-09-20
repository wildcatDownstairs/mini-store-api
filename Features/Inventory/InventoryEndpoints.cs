using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Inventory;

public sealed record StockAdjustment(int Quantity, string Reason);

public static class InventoryEndpoints
{
    public static void MapInventory(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/inventory")
            .WithTags("库存管理")
            .RequireAuthorization("AdminRead");
        group.MapGet(
            "/warehouses",
            async (StoreDbContext db, CancellationToken ct) =>
                await db
                    .Warehouses.AsNoTracking()
                    .OrderBy(w => w.Id)
                    .Select(w => new
                    {
                        id = w.PublicId,
                        w.Code,
                        w.Name,
                        w.Prefecture,
                        w.City,
                        w.Address,
                        quantityOnHand = w.Stocks.Sum(s => s.QuantityOnHand),
                        lowStockCount = w.Stocks.Count(s =>
                            s.QuantityOnHand - s.QuantityReserved < s.ReorderLevel
                        ),
                    })
                    .ToListAsync(ct)
        );
        group.MapGet(
            "/stocks",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
            {
                q.Validate();
                var source = db.Stocks.AsNoTracking();
                if (q.Warehouse.HasValue)
                    source = source.Where(s => s.Warehouse.PublicId == q.Warehouse);
                if (q.LowStock)
                    source = source.Where(s =>
                        s.QuantityOnHand - s.QuantityReserved < s.ReorderLevel
                    );
                if (q.Q is { Length: > 0 })
                {
                    var pattern = "%" + q.Q.Trim() + "%";
                    source = source.Where(s =>
                        EF.Functions.ILike(s.Variant.Sku, pattern)
                        || EF.Functions.ILike(s.Variant.Product.Name, pattern)
                    );
                }
                return await source
                    .OrderBy(s => s.WarehouseId)
                    .ThenBy(s => s.VariantId)
                    .Select(s => new
                    {
                        warehouseId = s.Warehouse.PublicId,
                        warehouse = s.Warehouse.Name,
                        variantId = s.Variant.PublicId,
                        s.Variant.Sku,
                        product = s.Variant.Product.Name,
                        variant = s.Variant.Name,
                        s.QuantityOnHand,
                        s.QuantityReserved,
                        availableQuantity = s.QuantityOnHand - s.QuantityReserved,
                        s.ReorderLevel,
                        s.UpdatedAt,
                    })
                    .PageAsync(q, ct);
            }
        );
        group.MapGet(
            "/stock-movements",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
            {
                q.Validate();
                var source = db.StockMovements.AsNoTracking();
                if (q.Warehouse.HasValue)
                    source = source.Where(m => m.Stock.Warehouse.PublicId == q.Warehouse);
                if (q.Q is { Length: > 0 })
                {
                    var pattern = "%" + q.Q.Trim() + "%";
                    source = source.Where(m => EF.Functions.ILike(m.Stock.Variant.Sku, pattern));
                }
                return await source
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Select(m => new
                    {
                        warehouseId = m.Stock.Warehouse.PublicId,
                        warehouse = m.Stock.Warehouse.Name,
                        variantId = m.Stock.Variant.PublicId,
                        m.Stock.Variant.Sku,
                        m.MovementType,
                        m.Quantity,
                        m.Note,
                        m.CreatedAt,
                        orderNumber = m.Order != null ? m.Order.OrderNumber : null,
                    })
                    .PageAsync(q, ct);
            }
        );
        group
            .MapPost(
                "/stocks/{warehouse:guid}/{variant:guid}/adjust",
                async (
                    Guid warehouse,
                    Guid variant,
                    StockAdjustment r,
                    StoreDbContext db,
                    CancellationToken ct
                ) =>
                {
                    Rules.Require(
                        r.Quantity is >= -999999 and <= 999999 && r.Quantity != 0,
                        "调整量须为非零整数且绝对值不超过 999999。"
                    );
                    var reason = Rules.Text(r.Reason, 500, "调整原因");
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var key =
                        await db
                            .Stocks.AsNoTracking()
                            .Where(s =>
                                s.Warehouse.PublicId == warehouse && s.Variant.PublicId == variant
                            )
                            .Select(s => new { s.WarehouseId, s.VariantId })
                            .SingleOrDefaultAsync(ct)
                        ?? throw ApiError.NotFound();
                    // 条件 UPDATE 把校验与写入合为一步，防止两个管理员先读后写造成库存丢失。
                    var changed = await db.Database.ExecuteSqlAsync(
                        $"UPDATE inventory.stocks SET quantity_on_hand=quantity_on_hand+{r.Quantity}, updated_at=clock_timestamp() WHERE warehouse_id={key.WarehouseId} AND variant_id={key.VariantId} AND quantity_on_hand::bigint+{r.Quantity} BETWEEN quantity_reserved AND 2147483647",
                        ct
                    );
                    if (changed != 1)
                        throw ApiError.Conflict("调整后库存不能低于已预占数量。");
                    db.StockMovements.Add(
                        new StockMovement
                        {
                            WarehouseId = key.WarehouseId,
                            VariantId = key.VariantId,
                            MovementType = "adjustment",
                            Quantity = r.Quantity,
                            ReferenceType = "adjustment",
                            Note = reason,
                            CreatedAt = DateTime.UtcNow,
                        }
                    );
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithSummary("原子调整实物库存并记录流水");
    }
}
