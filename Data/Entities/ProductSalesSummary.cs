using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

public partial class ProductSalesSummary
{
    /// <summary>
    /// 商品内部主键，关联 catalog.products.id。
    /// </summary>
    public long? ProductId { get; set; }

    /// <summary>
    /// 当前商品名称。
    /// </summary>
    public string? Product { get; set; }

    /// <summary>
    /// 已付款订单的商品总销售件数，包括后续退货订单，不扣除退货件数。
    /// </summary>
    public long? TotalQuantitySold { get; set; }

    /// <summary>
    /// 已付款订单的折扣前税前商品销售额，等于成交单价乘数量之和；不减退款，不含税费和运费。
    /// </summary>
    public decimal? GrossSales { get; set; }

    /// <summary>
    /// 包含该商品的已付款订单去重数量，包括后续退货订单。
    /// </summary>
    public long? OrderCount { get; set; }
}
