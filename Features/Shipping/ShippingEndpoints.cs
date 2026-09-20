using MiniStore.Common;

namespace MiniStore.Features.Shipping;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 ShippingService。</summary>
public static class ShippingEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapShipping(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminRead")
            .WithTags("发货与配送");

        admin
            .MapGet(
                "/shipments",
                ([AsParameters] ListQuery q, ShippingService service, CancellationToken ct) =>
                    service.ListAsync(q, ct)
            )
            .WithName("ListShipments")
            .WithSummary("分页查询物流记录")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status、warehouse；q 搜索订单号或运单号。warehouse 为仓库公开 UUID，按创建时间倒序。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);

        admin
            .MapPost(
                "/orders/{id:guid}/ship",
                (Guid id, ShipRequest r, ShippingService service, CancellationToken ct) =>
                    service.ShipAsync(id, r, ct)
            )
            .RequireAuthorization("AdminWrite")
            .WithName("ShipOrder")
            .WithSummary("从预占仓发货")
            .WithDescription(
                "仅 operator。id 是订单公开 UUID；订单必须已付款且 processing，warehouseId 必须匹配预占仓。一次事务扣减实物和预占库存、写流水与物流并转为 shipped；不支持拆仓或重复发货。成功返回物流 UUID 及 200。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);

        admin
            .MapPost(
                "/orders/{id:guid}/deliver",
                async (Guid id, ShippingService service, CancellationToken ct) =>
                {
                    await service.DeliverAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("DeliverOrder")
            .WithSummary("确认订单签收")
            .WithDescription(
                "仅 operator。订单必须为 shipped，物流为 shipped/in_transit；同步物流 delivered_at 和订单状态历史，成功返回 204。重复签收返回 409。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
    }
}
