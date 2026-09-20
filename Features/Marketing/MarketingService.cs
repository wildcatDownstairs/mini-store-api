using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Marketing;

/// <summary>处理优惠券功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class MarketingService(StoreDbContext db)
{
    /// <summary>分页筛选优惠券；active 指启用开关，是否在有效期内还需结算时校验。</summary>
    public async Task<TableModel<CouponDto>> ListAsync(ListQuery q, CancellationToken ct)
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
            .Select(c => new CouponDto(
                c.PublicId,
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
                c.IsActive
            ))
            .PageAsync(q, ct);
    }

    /// <summary>校验优惠规则并创建活动，不在创建时增加使用次数。</summary>
    public async Task<CouponCreatedDto> CreateAsync(CouponRequest r, CancellationToken ct)
    {
        var coupon = new Coupon { CreatedAt = DateTime.UtcNow };
        Fill(coupon, r);
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);
        return new CouponCreatedDto(coupon.PublicId);
    }

    /// <summary>锁定优惠券后更新；已有核销时禁止修改计价规则和有效期，避免历史业务失去解释依据。</summary>
    public async Task UpdateAsync(Guid id, CouponRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var coupon =
            await db
                .Coupons.FromSql($"SELECT * FROM marketing.coupons WHERE public_id={id} FOR UPDATE")
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
    }

    /// <summary>锁定优惠券后切换启停开关；已过期的优惠券不能再次启用。</summary>
    public async Task SetStatusAsync(Guid id, CouponStatusRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var coupon =
            await db
                .Coupons.FromSql($"SELECT * FROM marketing.coupons WHERE public_id={id} FOR UPDATE")
                .SingleOrDefaultAsync(ct)
            ?? throw ApiError.NotFound();
        Rules.Require(!r.IsActive || coupon.EndsAt > DateTime.UtcNow, "已过期优惠券不能启用。");
        coupon.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>统一校验优惠值、门槛、次数与有效期，并把请求时间转换为 UTC 写入实体。</summary>
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
