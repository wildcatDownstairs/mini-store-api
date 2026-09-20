using System.Security.Claims;
using MiniStore.Common;

namespace MiniStore.Features.Carts;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 CartService。</summary>
public static class CartEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapCart(this WebApplication app)
    {
        var group = app.MapGroup("/api/me/cart")
            .RequireAuthorization("Customer")
            .WithTags("购物车");
        group
            .MapGet(
                "",
                (ClaimsPrincipal user, CartService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.GetAsync(user.ActorId(), ct))
            )
            .WithName("GetMyCart")
            .WithSummary("读取我的购物车")
            .WithDescription(
                "需要客户身份。返回当前活动购物车、最新价格与可售库存；itemCount 是商品总件数。空购物车返回空 items 和 null totals。读取不预占库存。"
            )
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        group
            .MapPut(
                "/items/{id:guid}",
                async (
                    Guid id,
                    QuantityRequest r,
                    ClaimsPrincipal user,
                    CartService service,
                    CancellationToken ct
                ) =>
                {
                    await service.SetItemAsync(id, r, user.ActorId(), ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .WithName("SetMyCartItem")
            .WithSummary("设置购物车规格数量")
            .WithDescription(
                "需要客户身份。id 是规格公开 UUID；quantity 是最终数量（1～99），不是增量。价格由服务端读取，最多 50 种规格；加购不锁定价格或库存，余量不足返回 409。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        group
            .MapDelete(
                "/items/{id:guid}",
                async (Guid id, ClaimsPrincipal user, CartService service, CancellationToken ct) =>
                {
                    await service.RemoveItemAsync(id, user.ActorId(), ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .WithName("RemoveMyCartItem")
            .WithSummary("移除购物车规格")
            .WithDescription(
                "需要客户身份。id 是规格公开 UUID，只操作本人活动购物车；不存在的购物车项也返回 200，data 为 null。不会改变仓库库存。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);
    }
}
