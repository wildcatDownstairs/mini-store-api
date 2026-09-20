using MiniStore.Features.Customers;
using MiniStore.Features.Marketing;

namespace MiniStore.Features.Checkout;

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>AddressId</c>：当前客户收货地址的公开 UUID。</remarks>
/// <remarks><c>ShippingMethod</c>：standard 标准配送或 express 快捷配送。</remarks>
/// <param name="AddressId">本人收货地址的公开 UUID，不是内部 bigint。</param>
/// <param name="ShippingMethod">配送方式：standard 或 express。</param>
/// <param name="CouponCode">可选优惠码；为空表示不使用优惠券。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record QuoteRequest(Guid AddressId, string ShippingMethod, string? CouponCode);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>QuoteToken</c>：服务端报价返回的受保护令牌；幂等键另通过 Idempotency-Key 请求头传递。</remarks>
/// <param name="AddressId">本人收货地址的公开 UUID，不是内部 bigint。</param>
/// <param name="ShippingMethod">配送方式：standard 或 express。</param>
/// <param name="CouponCode">可选优惠码；为空表示不使用优惠券。</param>
/// <param name="QuoteToken">报价接口返回的受保护令牌，十分钟有效，不可自行构造。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record PlaceOrderRequest(
    Guid AddressId,
    string ShippingMethod,
    string? CouponCode,
    string QuoteToken
);

/// <summary>当前报价与十分钟有效令牌；报价不代表已预占库存，下单时仍需重新校验。</summary>
public sealed record QuoteResponse(
    string QuoteToken,
    DateTime ExpiresAt,
    IReadOnlyList<QuotedLine> Lines,
    Totals Totals
);

/// <summary>下单结果；Replayed 为 true 表示相同幂等请求已成功过，本次返回原订单。</summary>
public sealed record OrderCreated(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal GrandTotal,
    bool Replayed
);

/// <summary>受保护报价令牌的内部内容：客户内部主键与报价摘要，不作为普通 API 响应。</summary>
internal sealed record QuoteSignature(long CustomerId, string Digest);

/// <summary>结算内部工作数据，包含加载后的购物车、地址、优惠券及计价结果；不直接返回客户端。</summary>
internal sealed record CheckoutData(
    MiniStore.Features.Carts.Cart Cart,
    CustomerAddress Address,
    Coupon? Coupon,
    QuoteCore Quote
);

/// <summary>计价输入：Quantity 为件数，UnitPrice 为税前单价，TaxRate 使用 0.08 或 0.10。</summary>
public sealed record PriceLine(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate
);

/// <summary>计价后的一行：折扣、税额和 LineTotal 都是整行金额，而不是单件金额。</summary>
public sealed record QuotedLine(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal
);

/// <summary>整单金额：GrandTotal = Subtotal - DiscountTotal + TaxTotal + ShippingTotal，当前业务币种为 JPY。</summary>
public sealed record Totals(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal ShippingTotal,
    decimal GrandTotal,
    string Currency
);

/// <summary>纯计价结果：逐行金额和整单合计；不包含报价令牌或数据库实体。</summary>
public sealed record QuoteCore(IReadOnlyList<QuotedLine> Lines, Totals Totals);
