using MiniStore.Features.Orders;

namespace MiniStore.Features.Payments;

/// <summary>
/// 订单支付尝试：允许失败重试产生多条记录，记录支付渠道、金额及授权和扣款时间。
/// </summary>
public partial class Payment
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
    /// 订单内部主键，关联 sales.orders.id。
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 支付服务商：stripe、paypal、paypay 或 card_gateway（银行卡支付网关）。
    /// </summary>
    public string Provider { get; set; } = null!;

    /// <summary>
    /// 支付服务商交易编号；非空时全表唯一，用于对账及回调去重。
    /// </summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>
    /// 支付方式：credit_card 信用卡、paypal 贝宝、paypay 电子支付、bank_transfer 银行转账。
    /// </summary>
    public string Method { get; set; } = null!;

    /// <summary>
    /// 支付状态：pending 待处理、authorized 已授权、captured 已扣款、failed 失败、cancelled 已取消、refunded 已全额退款、partially_refunded 已部分退款。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 本次支付尝试的金额；当前业务采用整单支付，应与订单应付总额及币种一致；退款后保留原金额。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。
    /// </summary>
    public string Currency { get; set; } = null!;

    /// <summary>
    /// 支付授权成功时间；扣款前必须已有授权时间。
    /// </summary>
    public DateTime? AuthorizedAt { get; set; }

    /// <summary>
    /// 支付实际扣款成功时间；退款及部分退款后仍保留。
    /// </summary>
    public DateTime? CapturedAt { get; set; }

    /// <summary>
    /// 支付失败时间。
    /// </summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>关联的订单导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Order Order { get; set; } = null!;

    /// <summary>关联的退款记录集合；需通过 Include 或投影显式加载。</summary>
    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
