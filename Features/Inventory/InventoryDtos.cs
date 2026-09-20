namespace MiniStore.Features.Inventory;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Quantity</c>：库存增减量，不是目标库存值；正数入库，负数调减，不能为零。</remarks>
/// <param name="Quantity">库存增减量，非零整数，绝对值不超过 999999；不是调整后的库存总量。</param>
/// <param name="Reason">必填原因，最多 500 字符。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record StockAdjustment(int Quantity, string Reason);

/// <summary>仓库摘要：QuantityOnHand 为实物总件数，LowStockCount 为低于补货线的库存记录数。</summary>
public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string Prefecture,
    string City,
    string Address,
    int QuantityOnHand,
    int LowStockCount
);

/// <summary>单个仓库与规格的库存：AvailableQuantity = QuantityOnHand - QuantityReserved。</summary>
public sealed record StockDto(
    Guid WarehouseId,
    string Warehouse,
    Guid VariantId,
    string Sku,
    string Product,
    string Variant,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity,
    int ReorderLevel,
    DateTime UpdatedAt
);

/// <summary>库存流水展示；Quantity 是有符号的变动量，非订单流水没有 OrderNumber。</summary>
public sealed record StockMovementDto(
    Guid WarehouseId,
    string Warehouse,
    Guid VariantId,
    string Sku,
    string MovementType,
    int Quantity,
    string? Note,
    DateTime CreatedAt,
    string? OrderNumber
);
