using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 支付退款记录：待处理与已完成退款合计不得超过对应已扣款支付金额。
/// </summary>
public partial class Refund
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
    /// 支付记录内部主键，关联 payment.payments.id。
    /// </summary>
    public long PaymentId { get; set; }

    /// <summary>
    /// 本次退款金额，必须大于零；使用原支付币种，待处理与已完成退款合计不能超过已扣款金额。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 退款原因，不能为空值。
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// 退款状态：pending 待处理、completed 已完成、failed 失败。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 支付服务商退款编号；非空时全表唯一，用于退款对账。
    /// </summary>
    public string? ProviderRefundId { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 退款完成时间；仅 completed 状态非空。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    public virtual Payment Payment { get; set; } = null!;
}
