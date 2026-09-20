using MiniStore.Common;

namespace MiniStore.Features.Inventory;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 InventoryService。</summary>
public static class InventoryEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapInventory(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/inventory")
            .WithTags("库存管理")
            .RequireAuthorization("AdminRead");
        group
            .MapGet(
                "/warehouses",
                (InventoryService service, CancellationToken ct) => service.ListWarehousesAsync(ct)
            )
            .WithName("ListWarehouses")
            .WithSummary("列出仓库及库存概况")
            .WithDescription(
                "需要 operator 或 viewer。返回仓库公开 UUID、地址、实物库存汇总和低于补货线的库存记录数。"
            )
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
        group
            .MapGet(
                "/stocks",
                ([AsParameters] ListQuery q, InventoryService service, CancellationToken ct) =>
                    service.ListStocksAsync(q, ct)
            )
            .WithName("ListStocks")
            .WithSummary("分页查询仓库库存")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、warehouse、lowStock、q；q 搜索 SKU 或商品名，lowStock 比较可售量与补货线。可售量等于实物量减预占量。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
        group
            .MapGet(
                "/stock-movements",
                ([AsParameters] ListQuery q, InventoryService service, CancellationToken ct) =>
                    service.ListMovementsAsync(q, ct)
            )
            .WithName("ListStockMovements")
            .WithSummary("分页查询库存流水")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、warehouse、q；q 搜索 SKU。按时间倒序，订单关联流水返回订单号，非订单流水可能为空。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
        group
            .MapPost(
                "/stocks/{warehouse:guid}/{variant:guid}/adjust",
                async (
                    Guid warehouse,
                    Guid variant,
                    StockAdjustment r,
                    InventoryService service,
                    CancellationToken ct
                ) =>
                {
                    await service.AdjustAsync(warehouse, variant, r, ct);
                    return TypedResults.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("AdjustStock")
            .WithSummary("调整实物库存并记录流水")
            .WithDescription(
                "仅 operator。warehouse 与 variant 都是公开 UUID；quantity 为非零增减量，绝对值不超过 999999，必须填写原因。条件 UPDATE 防止实物库存低于预占量；成功返回 204。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
    }
}
