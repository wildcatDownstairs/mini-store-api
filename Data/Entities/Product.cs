using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 商品展示主体：保存品牌、名称、介绍及上下架状态；实际售卖规格见商品变体。
/// </summary>
public partial class Product
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
    /// 品牌内部主键，关联 catalog.brands.id。
    /// </summary>
    public long BrandId { get; set; }

    /// <summary>
    /// 商品当前显示名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 用于页面地址或 API 查询的可读唯一标识。
    /// </summary>
    public string Slug { get; set; } = null!;

    /// <summary>
    /// 商品详细介绍，可为空。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 商品状态：draft 草稿、active 上架、inactive 下架、archived 已归档。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 商品税前基础展示价格；实际下单单价来自所选变体。
    /// </summary>
    public decimal BasePrice { get; set; }

    /// <summary>
    /// 三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。
    /// </summary>
    public string Currency { get; set; } = null!;

    /// <summary>
    /// 商品发布时间；为空表示尚未发布。
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 软删除时间；为空表示未软删除。
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public virtual Brand Brand { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    /// <summary>商品图片集合；部分唯一索引只限制一张主图，不代表只能有一张图片。</summary>
    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } =
        new List<ProductReview>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } =
        new List<ProductVariant>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}
