using MiniStore.Features.Checkout;

namespace MiniStore.Features.Carts;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Quantity</c>：把数量设为这个值，不是累加；允许 1～99 件。</remarks>
/// <param name="Quantity">最终购物车数量，1～99；不是递增数量。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record QuantityRequest(int Quantity);

/// <summary>购物车条目：UnitPrice 是现价，PreviousUnitPrice 是加入或最后更新数量时保存的税前价。</summary>
public sealed record CartItemDto(
    Guid VariantId,
    Guid ProductId,
    string Sku,
    string ProductName,
    string VariantName,
    string Slug,
    int Quantity,
    decimal UnitPrice,
    decimal PreviousUnitPrice,
    string Currency,
    decimal DisplayPrice,
    decimal LineTotal,
    int AvailableQuantity,
    bool IsAvailable
);

/// <summary>活动购物车响应；ItemCount 为商品总件数，空车 Totals 为 null，非空合计暂按无券标准配送估算。</summary>
public sealed record CartDto(IEnumerable<CartItemDto> Items, int ItemCount, Totals? Totals);
