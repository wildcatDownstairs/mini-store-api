namespace MiniStore.Features.Products;

/// <summary>catalog.product_sales_summary 销量汇总视图的只读结果，不是业务写入实体。</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "EF Core 查询会自动创建实体或视图结果。"
)]
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
