using MiniStore.Common;

namespace MiniStore.Features.Marketing;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 MarketingService。</summary>
public static class MarketingEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapMarketing(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/coupons")
            .WithTags("优惠券管理")
            .RequireAuthorization("AdminRead");
        group
            .MapGet(
                "",
                ([AsParameters] ListQuery q, MarketingService service, CancellationToken ct) =>
                    service.ListAsync(q, ct)
            )
            .WithName("ListCoupons")
            .WithSummary("分页查询优惠券")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status；q 搜索优惠码或名称。status=active/inactive 只筛选启用标记，不等同于当前可核销。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
        group
            .MapPost(
                "",
                (CouponRequest r, MarketingService service, CancellationToken ct) =>
                    service.CreateAsync(r, ct)
            )
            .RequireAuthorization("AdminWrite")
            .WithName("CreateCoupon")
            .WithSummary("创建优惠券")
            .WithDescription(
                "仅 operator。percentage 的 10 表示优惠 10%，fixed 表示固定整数日元；校验门槛、上限、使用次数及有效时间。优惠码唯一，成功返回公开 UUID 及 200。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(500);
        group
            .MapPut(
                "/{id:guid}",
                async (Guid id, CouponRequest r, MarketingService service, CancellationToken ct) =>
                {
                    await service.UpdateAsync(id, r, ct);
                    return TypedResults.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("UpdateCoupon")
            .WithSummary("更新优惠券规则")
            .WithDescription(
                "仅 operator。id 是优惠券公开 UUID；已有核销记录后不能改变会影响历史业务的关键优惠规则，冲突返回 409。成功返回 204。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
        group
            .MapPatch(
                "/{id:guid}/status",
                async (
                    Guid id,
                    CouponStatusRequest r,
                    MarketingService service,
                    CancellationToken ct
                ) =>
                {
                    await service.SetStatusAsync(id, r, ct);
                    return TypedResults.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("SetCouponStatus")
            .WithSummary("启用或停用优惠券")
            .WithDescription(
                "仅 operator。按 isActive 更新启用标记；已过期优惠券不能启用，历史核销不删除。成功返回 204。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
    }
}
