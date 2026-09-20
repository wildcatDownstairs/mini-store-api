using System.Security.Claims;
using MiniStore.Common;

namespace MiniStore.Features.Orders;

public static class OrderEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapOrders(this WebApplication app)
    {
        var me = app.MapGroup("/api/me/orders")
            .RequireAuthorization("Customer")
            .WithTags("客户订单");

        var admin = app.MapGroup("/api/admin/orders")
            .RequireAuthorization("AdminRead")
            .WithTags("订单管理");

        me.MapGet(
                "",
                (
                    ClaimsPrincipal user,
                    OrderService service,
                    [AsParameters] ListQuery q,
                    CancellationToken ct
                ) => ApiResponse.OkAsync(service.ListAsync(q, user.ActorId(), ct))
            )
            .WithName("ListMyOrders")
            .WithSummary("分页读取我的订单")
            .WithDescription(
                "需要客户身份。支持 page、pageSize、q、status；q 搜索订单号或客户邮箱，按创建时间倒序。只能看到本人订单。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "",
                (OrderService service, [AsParameters] ListQuery q, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.ListAsync(q, null, ct))
            )
            .WithName("ListAdminOrders")
            .WithSummary("分页查询全部订单")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status；q 搜索订单号或客户邮箱，按创建时间倒序。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        me.MapGet(
                "/{id:guid}",
                (Guid id, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.DetailAsync(id, user.ActorId(), ct))
            )
            .WithName("GetMyOrder")
            .WithSummary("读取我的订单详情")
            .WithDescription(
                "需要客户身份。id 是订单公开 UUID；返回成交商品和地址快照、状态历史、支付退款及物流。订单不存在或属于他人都返回 404。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "/{id:guid}",
                (Guid id, OrderService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.DetailAsync(id, null, ct))
            )
            .WithName("GetAdminOrder")
            .WithSummary("读取后台订单详情")
            .WithDescription(
                "需要 operator 或 viewer。id 是订单公开 UUID；包含成交快照、支付退款、物流和 fulfillmentWarehouseId，后者用于选择预占仓发货。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        me.MapPost(
                "/{id:guid}/cancel",
                async (Guid id, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
                {
                    await service.CancelAsync(id, user.ActorId(), ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .WithName("CancelMyOrder")
            .WithSummary("取消我的未付款订单")
            .WithDescription(
                "需要客户身份。只允许本人 pending/confirmed 且未付款订单；同一事务释放预占、追加状态历史。重复取消已取消订单返回 409；优惠核销记录保留。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPost(
                "/{id:guid}/cancel",
                async (Guid id, OrderService service, CancellationToken ct) =>
                {
                    await service.CancelAsync(id, null, ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("CancelAdminOrder")
            .WithSummary("后台取消未付款订单")
            .WithDescription(
                "仅 operator。只允许 pending/confirmed 且未付款订单；释放预占并保存状态历史，重复取消返回 409。已付款订单应走退款流程。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPost(
                "/{id:guid}/confirm",
                async (Guid id, OrderService service, CancellationToken ct) =>
                {
                    await service.ChangeAsync(id, "pending", "confirmed", ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("ConfirmOrder")
            .WithSummary("确认待确认订单")
            .WithDescription(
                "仅 operator。只允许 pending → confirmed，其他状态返回 409；追加状态历史，成功返回 200，data 为 null。当前下单接口已直接完成该确认步骤。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPost(
                "/{id:guid}/process",
                async (Guid id, OrderService service, CancellationToken ct) =>
                {
                    await service.ChangeAsync(id, "paid", "processing", ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("ProcessOrder")
            .WithSummary("将已付款订单转为配货")
            .WithDescription(
                "仅 operator。只允许 paid → processing，追加状态历史，成功返回 200，data 为 null；随后通过发货接口扣减库存。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);
    }
}
