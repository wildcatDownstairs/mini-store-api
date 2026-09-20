using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

public partial class ProductInventorySummary
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
    /// 商品变体内部主键，关联 catalog.product_variants.id。
    /// </summary>
    public long? VariantId { get; set; }

    /// <summary>
    /// 当前商品变体名称。
    /// </summary>
    public string? Variant { get; set; }

    /// <summary>
    /// 库存管理编码，唯一标识一个商品变体。
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    /// 该变体跨仓库的实物库存总量。
    /// </summary>
    public long? TotalStock { get; set; }

    /// <summary>
    /// 该变体跨仓库的预占库存总量。
    /// </summary>
    public long? ReservedStock { get; set; }

    /// <summary>
    /// 该变体跨仓库的可售库存，等于实物库存总量减预占库存总量。
    /// </summary>
    public long? AvailableStock { get; set; }
}
