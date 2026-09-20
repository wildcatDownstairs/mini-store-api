namespace MiniStore.Features.Shipping;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>WarehouseId</c>：发货仓库的公开 UUID，必须匹配订单实际预占的仓库。</remarks>
/// <param name="WarehouseId">仓库公开 UUID，必须与订单预占仓一致。</param>
/// <param name="Carrier">承运商：Yamato、Sagawa 或 Japan Post。</param>
/// <param name="TrackingNumber">唯一运单号，8～30 位字母、数字或连字符。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record ShipRequest(Guid WarehouseId, string Carrier, string TrackingNumber);

/// <summary>后台物流列表，关联订单与仓库公开标识；发货和送达时间可以为空。</summary>
public sealed record ShipmentSummaryDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Carrier,
    string? TrackingNumber,
    string Status,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    Guid WarehouseId,
    string Warehouse
);

/// <summary>发货成功后返回物流公开标识；不表示承运商系统已收到真实请求。</summary>
public sealed record ShipmentCreatedDto(Guid Id);
