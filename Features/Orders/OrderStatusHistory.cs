namespace MiniStore.Features.Orders;

/// <summary>
/// 订单状态流转记录：按时间保存前后状态及变更原因，供追踪和审计。
/// </summary>
public partial class OrderStatusHistory
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
    /// 变更前订单状态；首条记录为空，表示订单刚创建。
    /// </summary>
    public string? FromStatus { get; set; }

    /// <summary>
    /// 变更后订单状态：pending 待确认、confirmed 已确认、paid 已付款、processing 处理中、shipped 已发货、delivered 已送达、cancelled 已取消、returned 已退货；仅允许约束定义的状态流转。
    /// </summary>
    public string ToStatus { get; set; } = null!;

    /// <summary>
    /// 订单状态变更原因，可为空。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 本次订单状态变更发生时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>关联的订单导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Order Order { get; set; } = null!;
}
