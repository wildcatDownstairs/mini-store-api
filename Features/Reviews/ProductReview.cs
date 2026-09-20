using MiniStore.Features.Customers;
using MiniStore.Features.Orders;
using MiniStore.Features.Products;

namespace MiniStore.Features.Reviews;

/// <summary>
/// 商品评价：保存评分、文字与审核状态；已验证购买评价须关联真实购买明细及收货时间。
/// </summary>
public partial class ProductReview
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
    /// 所属客户的内部主键，关联 account.customers.id。
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// 商品内部主键，关联 catalog.products.id。
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// 购买明细内部主键，关联 sales.order_items.id；非空时唯一，限制同一订单项重复评价。
    /// </summary>
    public long? OrderItemId { get; set; }

    /// <summary>
    /// 商品评分，取值为一至五星。
    /// </summary>
    public short Rating { get; set; }

    /// <summary>
    /// 评价标题，可为空。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 评价文字内容，可为空，允许仅提交评分。
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 是否为已验证购买评价；为真时须关联同客户同商品的已收货订单项，由触发器校验。
    /// </summary>
    public bool IsVerifiedPurchase { get; set; }

    /// <summary>
    /// 评价审核状态：pending 待审核、published 已发布、rejected 已拒绝。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>关联的客户导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Customer Customer { get; set; } = null!;

    /// <summary>关联的订单项快照导航属性；与外键 ID 不同，它表示关联对象。没有关联记录或尚未加载时可能为空。</summary>
    public virtual OrderItem? OrderItem { get; set; }

    /// <summary>关联的商品导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Product Product { get; set; } = null!;
}
