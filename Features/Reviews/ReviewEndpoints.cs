using System.Security.Claims;
using MiniStore.Common;

namespace MiniStore.Features.Reviews;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 ReviewService。</summary>
public static class ReviewEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapReviews(this WebApplication app)
    {
        app.MapGet(
                "/api/store/products/{id:guid}/reviews",
                (
                    Guid id,
                    [AsParameters] ListQuery q,
                    ReviewService service,
                    CancellationToken ct
                ) => service.ListProductAsync(id, q, ct)
            )
            .WithTags("商品评价")
            .WithName("ListProductReviews")
            .WithSummary("分页读取商品公开评价")
            .WithDescription(
                "公开接口。id 是商品公开 UUID，仅使用 page、pageSize；只展示仍上架商品的 published 评价，作者仅显示姓氏加称呼。商品不可见或不存在时返回空页。"
            )
            .ProducesProblem(400)
            .ProducesProblem(500);

        app.MapPost(
                "/api/me/orders/{id:guid}/items/{itemId:guid}/review",
                (
                    Guid id,
                    Guid itemId,
                    ReviewRequest r,
                    ClaimsPrincipal user,
                    ReviewService service,
                    CancellationToken ct
                ) => service.CreateAsync(id, itemId, r, user.ActorId(), ct)
            )
            .RequireAuthorization("Customer")
            .WithTags("商品评价")
            .WithName("CreateMyReview")
            .WithSummary("评价已签收的订单商品")
            .WithDescription(
                "需要客户身份。id 和 itemId 分别为本人订单与其订单项的公开 UUID。仅 delivered 可评价，rating 为 1～5；每个订单项只能评价一次，新评价为 pending，审核后才公开。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
        var admin = app.MapGroup("/api/admin/reviews")
            .RequireAuthorization("AdminRead")
            .WithTags("评价审核");

        admin
            .MapGet(
                "",
                ([AsParameters] ListQuery q, ReviewService service, CancellationToken ct) =>
                    service.ListAdminAsync(q, ct)
            )
            .WithName("ListAdminReviews")
            .WithSummary("分页查询待审核及历史评价")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status；q 搜索商品名或评价正文，按创建时间倒序。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);

        admin
            .MapPost(
                "/{id:guid}/review",
                async (Guid id, ReviewDecision r, ReviewService service, CancellationToken ct) =>
                {
                    await service.ModerateAsync(id, r, ct);
                    return TypedResults.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("ModerateReview")
            .WithSummary("发布或拒绝待审核评价")
            .WithDescription(
                "仅 operator。id 是评价公开 UUID；status 只接受 published/rejected。条件更新只处理 pending，记录不存在或已处理返回 409，成功返回 204。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(500);
    }
}
