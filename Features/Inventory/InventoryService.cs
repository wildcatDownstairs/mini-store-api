using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Inventory;

/// <summary>处理库存功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class InventoryService(StoreDbContext db)
{
    /// <summary>列出仓库及其库存汇总、缺货预警数量；预警数量统计库存记录，不是缺少的件数。</summary>
    public async Task<List<WarehouseDto>> ListWarehousesAsync(CancellationToken ct)
    {
        return await db
            .Warehouses.AsNoTracking()
            .OrderBy(w => w.Id)
            .Select(w => new WarehouseDto(
                w.PublicId,
                w.Code,
                w.Name,
                w.Prefecture,
                w.City,
                w.Address,
                w.Stocks.Sum(s => s.QuantityOnHand),
                w.Stocks.Count(s => s.QuantityOnHand - s.QuantityReserved < s.ReorderLevel)
            ))
            .ToListAsync(ct);
    }

    /// <summary>按仓库和规格查询库存，可筛选可售量低于补货线的记录；稳定排序后数据库分页。</summary>
    public async Task<TableModel<StockDto>> ListStocksAsync(ListQuery q, CancellationToken ct)
    {
        q.Validate();
        var source = db.Stocks.AsNoTracking();
        if (q.Warehouse.HasValue)
            source = source.Where(s => s.Warehouse.PublicId == q.Warehouse);
        if (q.LowStock)
            source = source.Where(s => s.QuantityOnHand - s.QuantityReserved < s.ReorderLevel);
        if (q.Q is { Length: > 0 })
        {
            var pattern = Rules.ContainsPattern(q.Q);
            source = source.Where(s =>
                EF.Functions.ILike(s.Variant.Sku, pattern)
                || EF.Functions.ILike(s.Variant.Product.Name, pattern)
            );
        }
        return await source
            .OrderBy(s => s.WarehouseId)
            .ThenBy(s => s.VariantId)
            .Select(s => new StockDto(
                s.Warehouse.PublicId,
                s.Warehouse.Name,
                s.Variant.PublicId,
                s.Variant.Sku,
                s.Variant.Product.Name,
                s.Variant.Name,
                s.QuantityOnHand,
                s.QuantityReserved,
                s.QuantityOnHand - s.QuantityReserved,
                s.ReorderLevel,
                s.UpdatedAt
            ))
            .PageAsync(q, ct);
    }

    /// <summary>分页读取库存变动审计记录；订单号通过关联读取，非订单流水可能没有订单号。</summary>
    public async Task<TableModel<StockMovementDto>> ListMovementsAsync(
        ListQuery q,
        CancellationToken ct
    )
    {
        q.Validate();
        var source = db.StockMovements.AsNoTracking();
        if (q.Warehouse.HasValue)
            source = source.Where(m => m.Stock.Warehouse.PublicId == q.Warehouse);
        if (q.Q is { Length: > 0 })
        {
            var pattern = Rules.ContainsPattern(q.Q);
            source = source.Where(m => EF.Functions.ILike(m.Stock.Variant.Sku, pattern));
        }
        return await source
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new StockMovementDto(
                m.Stock.Warehouse.PublicId,
                m.Stock.Warehouse.Name,
                m.Stock.Variant.PublicId,
                m.Stock.Variant.Sku,
                m.MovementType,
                m.Quantity,
                m.Note,
                m.CreatedAt,
                m.Order != null ? m.Order.OrderNumber : null
            ))
            .PageAsync(q, ct);
    }

    /// <summary>按增减量原子调整实物库存，并在同一事务记录原因；不能占用其他订单已预占的库存。</summary>
    public async Task AdjustAsync(
        Guid warehouse,
        Guid variant,
        StockAdjustment r,
        CancellationToken ct
    )
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
                .Where(s => s.Warehouse.PublicId == warehouse && s.Variant.PublicId == variant)
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
    }
}
