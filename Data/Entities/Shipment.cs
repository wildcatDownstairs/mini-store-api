using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 订单物流记录：保存发货仓、承运商、运单号与配送时间；订单可拆成多个包裹。
/// </summary>
public partial class Shipment
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
    /// 仓库内部主键，关联 inventory.warehouses.id。
    /// </summary>
    public long WarehouseId { get; set; }

    /// <summary>
    /// 承运商名称，例如 Yamato、Sagawa 或 Japan Post。
    /// </summary>
    public string Carrier { get; set; } = null!;

    /// <summary>
    /// 物流运单号；非空时全表唯一。
    /// </summary>
    public string? TrackingNumber { get; set; }

    /// <summary>
    /// 物流状态：pending 待处理、ready 待发货、shipped 已发货、in_transit 运输中、delivered 已送达、failed 配送失败、returned 已退回。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 实际发货时间。
    /// </summary>
    public DateTime? ShippedAt { get; set; }

    /// <summary>
    /// 签收或送达时间，不得早于发货时间。
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
