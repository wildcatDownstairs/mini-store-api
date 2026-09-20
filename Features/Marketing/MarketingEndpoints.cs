using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Marketing;

public sealed record CouponRequest(
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    int? UsageLimit,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive
);

public sealed record CouponStatusRequest(bool IsActive);

public static class MarketingEndpoints
{
    public static void MapMarketing(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/coupons")
            .WithTags("优惠券管理")
            .RequireAuthorization("AdminRead");
        group.MapGet(
            "",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
            {
                q.Validate();
                var source = db.Coupons.AsNoTracking();
                if (q.Status == "active")
                    source = source.Where(c => c.IsActive);
                if (q.Status == "inactive")
                    source = source.Where(c => !c.IsActive);
                if (q.Q is { Length: > 0 })
                {
                    var pattern = "%" + q.Q.Trim() + "%";
                    source = source.Where(c =>
                        EF.Functions.ILike(c.Code, pattern) || EF.Functions.ILike(c.Name, pattern)
                    );
                }
                return await source
                    .OrderByDescending(c => c.CreatedAt)
                    .ThenByDescending(c => c.Id)
                    .Select(c => new
                    {
                        id = c.PublicId,
                        c.Code,
                        c.Name,
                        c.DiscountType,
                        c.DiscountValue,
                        c.MinOrderAmount,
                        c.MaxDiscountAmount,
                        c.UsageLimit,
                        c.UsedCount,
                        c.StartsAt,
                        c.EndsAt,
                        c.IsActive,
                    })
                    .PageAsync(q, ct);
            }
        );
        group
            .MapPost(
                "",
                async (CouponRequest r, StoreDbContext db, CancellationToken ct) =>
                {
                    var coupon = new Coupon { CreatedAt = DateTime.UtcNow };
                    Fill(coupon, r);
                    db.Coupons.Add(coupon);
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(new { id = coupon.PublicId });
                }
            )
            .RequireAuthorization("AdminWrite");
        group
            .MapPut(
                "/{id:guid}",
                async (Guid id, CouponRequest r, StoreDbContext db, CancellationToken ct) =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var coupon =
                        await db
                            .Coupons.FromSql(
                                $"SELECT * FROM marketing.coupons WHERE public_id={id} FOR UPDATE"
                            )
                            .SingleOrDefaultAsync(ct)
                        ?? throw ApiError.NotFound();
                    // 已核销券的计价规则保留原样，避免历史订单与核销金额失去解释依据。
                    if (
                        coupon.UsedCount > 0
                        && (
                            coupon.Code != r.Code?.Trim().ToUpperInvariant()
                            || coupon.DiscountType != r.DiscountType
                            || coupon.DiscountValue != r.DiscountValue
                            || coupon.MinOrderAmount != r.MinOrderAmount
                            || coupon.MaxDiscountAmount != r.MaxDiscountAmount
                            || coupon.StartsAt != r.StartsAt.UtcDateTime
                            || coupon.EndsAt != r.EndsAt.UtcDateTime
                        )
                    )
                        throw ApiError.Conflict(
                            "已使用优惠券不能更改计价规则或有效期，请新建活动；可以修改名称、启停和次数上限。"
                        );
                    Fill(coupon, r);
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
        group
            .MapPatch(
                "/{id:guid}/status",
                async (Guid id, CouponStatusRequest r, StoreDbContext db, CancellationToken ct) =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var coupon =
                        await db
                            .Coupons.FromSql(
                                $"SELECT * FROM marketing.coupons WHERE public_id={id} FOR UPDATE"
                            )
                            .SingleOrDefaultAsync(ct)
                        ?? throw ApiError.NotFound();
                    Rules.Require(
                        !r.IsActive || coupon.EndsAt > DateTime.UtcNow,
                        "已过期优惠券不能启用。"
                    );
                    coupon.IsActive = r.IsActive;
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
    }

    private static void Fill(Coupon c, CouponRequest r)
    {
        var code = Rules.Text(r.Code, 40, "优惠码").ToUpperInvariant();
        Rules.Require(Regex.IsMatch(code, "^[A-Z0-9_-]{3,40}$"), "优惠码格式无效。");
        Rules.Require(r.DiscountType is "fixed" or "percentage", "优惠类型无效。");
        Rules.Money(r.DiscountValue, "优惠值", true);
        Rules.Money(r.MinOrderAmount, "门槛金额");
        if (r.MaxDiscountAmount.HasValue)
            Rules.Money(r.MaxDiscountAmount.Value, "最高优惠", true);
        Rules.Require(
            r.DiscountType != "percentage" || r.DiscountValue <= 100,
            "百分比不能超过 100。"
        );
        Rules.Require(
            r.UsageLimit == null || r.UsageLimit > 0 && r.UsageLimit >= c.UsedCount,
            "使用上限不能低于已使用次数。"
        );
        Rules.Require(
            r.EndsAt > r.StartsAt && (!r.IsActive || r.EndsAt > DateTime.UtcNow),
            "有效期须为 UTC 时间，结束时间必须晚于开始；过期券不能启用。"
        );
        c.Code = code;
        c.Name = Rules.Text(r.Name, 120, "活动名称");
        c.DiscountType = r.DiscountType;
        c.DiscountValue = r.DiscountValue;
        c.MinOrderAmount = r.MinOrderAmount;
        c.MaxDiscountAmount = r.MaxDiscountAmount;
        c.UsageLimit = r.UsageLimit;
        c.StartsAt = r.StartsAt.UtcDateTime;
        c.EndsAt = r.EndsAt.UtcDateTime;
        c.IsActive = r.IsActive;
    }
}
