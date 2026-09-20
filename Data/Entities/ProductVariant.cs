using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 商品变体：每行代表一个可售规格及其唯一 SKU，保存规格售价与属性。
/// </summary>
public partial class ProductVariant
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
    /// 商品内部主键，关联 catalog.products.id。
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// 库存管理编码，唯一标识一个商品变体。
    /// </summary>
    public string Sku { get; set; } = null!;

    /// <summary>
    /// 商品条码；非空时必须唯一。
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 变体规格显示名称，例如黑色／512GB。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 商品变体税前售价，非负，币种由 currency 指定。
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。
    /// </summary>
    public string Currency { get; set; } = null!;

    /// <summary>
    /// 变体规格属性的 JSON 对象，例如颜色、尺码或容量；核心关联仍使用外键。
    /// </summary>
    public string Attributes { get; set; } = null!;

    /// <summary>
    /// 商品变体重量，单位为克，必须非负。
    /// </summary>
    public int WeightGrams { get; set; }

    /// <summary>
    /// 该变体是否启用；可售性还需结合商品状态和可售库存判断。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
