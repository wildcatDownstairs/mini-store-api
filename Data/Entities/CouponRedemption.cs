using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 优惠券核销记录：关联客户和订单，保存实际优惠金额；当前每笔订单最多使用一张优惠券。
/// </summary>
public partial class CouponRedemption
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 优惠券内部主键，关联 marketing.coupons.id。
    /// </summary>
    public long CouponId { get; set; }

    /// <summary>
    /// 所属客户的内部主键，关联 account.customers.id。
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// 订单内部主键，关联 sales.orders.id。
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 本次订单实际使用优惠券减免的税前商品金额，应等于该订单 discount_total。
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// 优惠券实际核销时间。
    /// </summary>
    public DateTime RedeemedAt { get; set; }

    public virtual Coupon Coupon { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
