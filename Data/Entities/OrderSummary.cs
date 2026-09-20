using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

public partial class OrderSummary
{
    /// <summary>
    /// 订单内部主键，来自 sales.orders.id。
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 订单对外 UUID 标识，来自 sales.orders.public_id。
    /// </summary>
    public Guid? PublicId { get; set; }

    /// <summary>
    /// 唯一的可读订单编号，供客户查询和业务对账使用。
    /// </summary>
    public string? OrderNumber { get; set; }

    /// <summary>
    /// 订单客户姓名，由姓氏和名字拼接。
    /// </summary>
    public string? Customer { get; set; }

    /// <summary>
    /// 订单当前状态；含义与 sales.orders.status 相同。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 订单内商品总件数，等于明细 quantity 之和，不是明细行数。
    /// </summary>
    public long? ItemCount { get; set; }

    /// <summary>
    /// 折扣前税前商品金额，等于所有订单项数量乘单价之和。
    /// </summary>
    public decimal? Subtotal { get; set; }

    /// <summary>
    /// 订单优惠总额，对应 sales.orders.discount_total。
    /// </summary>
    public decimal? Discount { get; set; }

    /// <summary>
    /// 订单商品税额总计，对应 sales.orders.tax_total。
    /// </summary>
    public decimal? Tax { get; set; }

    /// <summary>
    /// 订单配送费用，对应 sales.orders.shipping_total。
    /// </summary>
    public decimal? Shipping { get; set; }

    /// <summary>
    /// 订单应付总额，等于商品小计减优惠总额加税额加配送费。
    /// </summary>
    public decimal? GrandTotal { get; set; }

    /// <summary>
    /// 客户下单时间。
    /// </summary>
    public DateTime? PlacedAt { get; set; }
}
