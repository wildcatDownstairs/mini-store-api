using MiniStore.Features.Products;

namespace MiniStore.Features.Carts;

/// <summary>
/// 购物车商品项：记录变体、数量及当时单价；结算时应重新校验价格与库存。
/// </summary>
public partial class CartItem
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 购物车内部主键，关联 sales.carts.id。
    /// </summary>
    public long CartId { get; set; }

    /// <summary>
    /// 商品变体内部主键，关联 catalog.product_variants.id。
    /// </summary>
    public long VariantId { get; set; }

    /// <summary>
    /// 购买数量，必须为正整数。
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 加入购物车时记录的变体税前单价；结算时需重新确认，不代表已锁定成交价格。
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>关联的购物车导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Cart Cart { get; set; } = null!;

    /// <summary>关联的商品规格导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual ProductVariant Variant { get; set; } = null!;
}
