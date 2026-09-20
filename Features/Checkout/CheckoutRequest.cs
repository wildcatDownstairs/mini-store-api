using MiniStore.Features.Customers;
using MiniStore.Features.Orders;

namespace MiniStore.Features.Checkout;

/// <summary>
/// 已完成下单请求的幂等记录：相同客户和键返回原订单；同键不同请求拒绝。与订单在同一事务提交。
/// </summary>
public partial class CheckoutRequest
{
    /// <summary>
    /// 发起下单的客户内部主键。
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// 客户端 Idempotency-Key，长度最多 128，客户端重试需复用。
    /// </summary>
    public string RequestKey { get; set; } = null!;

    /// <summary>
    /// 请求内容的 SHA-256 摘要，避免同一幂等键被用于不同请求。
    /// </summary>
    public string RequestHash { get; set; } = null!;

    /// <summary>
    /// 成功创建的订单内部主键。
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 幂等请求成功提交时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>关联的客户导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Customer Customer { get; set; } = null!;

    /// <summary>关联的订单导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Order Order { get; set; } = null!;
}
