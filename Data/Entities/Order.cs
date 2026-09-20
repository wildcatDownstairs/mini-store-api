using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 订单主表：保存成交金额、订单状态与关键时间；与明细、支付和物流共同组成订单业务。
/// </summary>
public partial class Order
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
    /// 唯一的可读订单编号，供客户查询和业务对账使用。
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 所属客户的内部主键，关联 account.customers.id。
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// 订单状态：pending 待确认、confirmed 已确认、paid 已付款、processing 处理中、shipped 已发货、delivered 已送达、cancelled 已取消、returned 已退货。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。
    /// </summary>
    public string Currency { get; set; } = null!;

    /// <summary>
    /// 折扣前税前商品金额，等于所有订单项数量乘单价之和。
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// 订单优惠总额，等于订单项分摊折扣之和，与优惠券实际核销金额对应。
    /// </summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>
    /// 折后商品消费税总额，等于订单项税额之和。
    /// </summary>
    public decimal TaxTotal { get; set; }

    /// <summary>
    /// 最终收取的配送费，不参与商品折扣计算。
    /// </summary>
    public decimal ShippingTotal { get; set; }

    /// <summary>
    /// 订单应付总额，等于商品小计减优惠总额加税额加配送费。
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// 客户下单时间。
    /// </summary>
    public DateTime PlacedAt { get; set; }

    /// <summary>
    /// 订单付款成功时间；为空表示尚未记录付款成功。
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// 订单取消时间；仅 cancelled 状态非空。
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// 订单送达完成时间；delivered 和 returned 状态必须有值，退货后保留原完成时间。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CheckoutRequest> CheckoutRequests { get; set; } =
        new List<CheckoutRequest>();

    public virtual CouponRedemption? CouponRedemption { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<OrderAddress> OrderAddresses { get; set; } =
        new List<OrderAddress>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } =
        new List<OrderStatusHistory>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } =
        new List<StockMovement>();
}
