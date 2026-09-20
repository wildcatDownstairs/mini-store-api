using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 优惠券规则：定义折扣、门槛、有效期和使用次数限制。
/// </summary>
public partial class Coupon
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。
    /// </summary>
    public Guid PublicId { get; set; }

    /// <summary>
    /// 唯一优惠码，供客户结算时输入；当前唯一约束区分大小写。
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// 优惠券活动显示名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 优惠类型：percentage 按百分比减免，fixed 固定金额减免。
    /// </summary>
    public string DiscountType { get; set; } = null!;

    /// <summary>
    /// 优惠数值；percentage 时为减免百分比（10 表示减免 10%，即九折），fixed 时为固定减免金额。
    /// </summary>
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// 使用优惠券所需的折扣前税前商品最低金额，不含运费。
    /// </summary>
    public decimal MinOrderAmount { get; set; }

    /// <summary>
    /// 单次优惠金额上限；为空表示不单独限制，但优惠不得超过商品小计。
    /// </summary>
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>
    /// 优惠券最多可核销次数；为空表示不限次数。
    /// </summary>
    public int? UsageLimit { get; set; }

    /// <summary>
    /// 已核销次数，应与核销记录数一致，由业务事务维护。
    /// </summary>
    public int UsedCount { get; set; }

    /// <summary>
    /// 优惠券有效期开始时间。
    /// </summary>
    public DateTime StartsAt { get; set; }

    /// <summary>
    /// 优惠券有效期结束时间，必须晚于开始时间。
    /// </summary>
    public DateTime EndsAt { get; set; }

    /// <summary>
    /// 优惠券是否启用；核销仍需校验有效期、金额门槛及使用次数。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CouponRedemption> CouponRedemptions { get; set; } =
        new List<CouponRedemption>();
}
