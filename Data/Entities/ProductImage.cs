using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 商品图片：可关联具体变体；不关联变体时作为商品通用图片。
/// </summary>
public partial class ProductImage
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 商品内部主键，关联 catalog.products.id。
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// 可选的变体内部主键；为空表示商品通用图片，非空时必须属于同一商品。
    /// </summary>
    public long? VariantId { get; set; }

    /// <summary>
    /// 图片资源地址；种子数据使用示例地址。
    /// </summary>
    public string Url { get; set; } = null!;

    /// <summary>
    /// 图片替代文字，供图片无法加载或辅助阅读时使用。
    /// </summary>
    public string AltText { get; set; } = null!;

    /// <summary>
    /// 显示排序值，通常按升序展示。
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否为商品主图。
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ProductVariant? ProductVariant { get; set; }
}
