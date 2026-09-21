using System.Text.Json.Serialization;

namespace MiniStore.Features.Payments;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <param name="Success">true 模拟成功收款；false 记录一次失败尝试，不产生真实资金流。此字段必填，省略时返回 400，不会默认记成失败。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record SimulatedPaymentRequest([property: JsonRequired] bool Success);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>PaymentId</c>：已收款支付记录的公开 UUID，不是订单 UUID。</remarks>
/// <remarks><c>Amount</c>：本次申请退款金额，整数日元；待审核申请也会占用可退额度。</remarks>
/// <param name="PaymentId">成功收款的支付记录公开 UUID。</param>
/// <param name="Amount">申请退款的正整数 JPY；不能超过扣除待审核及已完成退款后的可退额。</param>
/// <param name="Reason">必填原因，最多 500 字符。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record RefundRequest(Guid PaymentId, decimal Amount, string Reason);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Approved</c>：true 在开发模拟模式完成退款；false 拒绝申请并将状态记为 failed。</remarks>
/// <param name="Approved">是否批准退款；批准仅在开发模拟模式下可用。此字段必填，省略时返回 400，不会默认拒绝。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record RefundDecision([property: JsonRequired] bool Approved);

/// <summary>支付列表；AvailableRefund 从已收款金额扣除已完成及待审核退款，未收款时为零。</summary>
public sealed record PaymentSummaryDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Provider,
    string? ProviderTransactionId,
    string Method,
    string Status,
    decimal Amount,
    string Currency,
    DateTime? CapturedAt,
    DateTime CreatedAt,
    decimal Refunded,
    decimal PendingRefund,
    decimal AvailableRefund
);

/// <summary>退款列表项，包含原支付、订单、币种及审核完成时间。</summary>
public sealed record RefundSummaryDto(
    Guid Id,
    Guid PaymentId,
    Guid OrderId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    string Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

/// <summary>新建退款申请的公开标识及 pending 状态，创建申请不等于已经退款。</summary>
public sealed record RefundCreatedDto(Guid Id, string Status);

// 两种模拟支付结果共用一个 DTO；省略不适用的字段，保持原有 JSON 响应格式。
/// <summary>模拟付款结果；新尝试返回 Success，已付款重试返回 AlreadyPaid，不适用的字段省略。</summary>
public sealed record PaymentSimulationDto(
    string Status,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    )]
        bool? AlreadyPaid = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    )]
        bool? Success = null
);
