using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 订单商品快照：保留下单时的 SKU、名称、成交价格及折扣税额，后续商品修改不影响历史订单。
/// </summary>
public partial class OrderItem
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 订单内部主键，关联 sales.orders.id。
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 商品内部主键，关联 catalog.products.id。
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// 商品变体内部主键，关联 catalog.product_variants.id。
    /// </summary>
    public long VariantId { get; set; }

    /// <summary>
    /// 下单时的商品变体 SKU 快照，不随当前 SKU 修改而更新。
    /// </summary>
    public string Sku { get; set; } = null!;

    /// <summary>
    /// 下单时的商品名称快照，不随商品改名而更新。
    /// </summary>
    public string ProductName { get; set; } = null!;

    /// <summary>
    /// 下单时的变体名称快照，不随规格改名而更新。
    /// </summary>
    public string VariantName { get; set; } = null!;

    /// <summary>
    /// 购买数量，必须为正整数。
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 下单成交时的税前单价快照，后续调价不影响该值。
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 分摊至此订单项的整行折扣金额，不是单件折扣，不能超过数量乘单价。
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// 此订单项在折扣分摊后的消费税额；为整行税额。
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// 订单项含税净额，等于数量乘税前单价减整行折扣加整行税额，不含运费。
    /// </summary>
    public decimal LineTotal { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 订单项对外 UUID 标识；用于提交购买评价，仍须校验订单归属。
    /// </summary>
    public Guid PublicId { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductReview? ProductReview { get; set; }

    public virtual ProductVariant ProductVariant { get; set; } = null!;
}
