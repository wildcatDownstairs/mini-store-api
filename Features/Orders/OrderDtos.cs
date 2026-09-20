namespace MiniStore.Features.Orders;

/// <summary>订单详情聚合响应；商品和地址使用下单快照，付款、退款、物流来自各自记录。</summary>
public sealed record OrderDetailDto(
    Guid? FulfillmentWarehouseId,
    Guid Id,
    string OrderNumber,
    string Status,
    string Currency,
    DateTime PlacedAt,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    DateTime? CompletedAt,
    OrderCustomerDto Customer,
    OrderTotalsDto Totals,
    IEnumerable<OrderItemDto> Items,
    IEnumerable<OrderAddressDto> Addresses,
    IEnumerable<OrderHistoryDto> History,
    IEnumerable<OrderPaymentDto> Payments,
    IEnumerable<OrderShipmentDto> Shipments
);

/// <summary>订单所属客户的当前显示信息；这不是下单时客户姓名的历史快照。</summary>
public sealed record OrderCustomerDto(Guid Id, string Name, string Email);

/// <summary>订单存储的历史成交金额；不能用当前商品价格重新计算替换。</summary>
public sealed record OrderTotalsDto(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal ShippingTotal,
    decimal GrandTotal,
    string Currency
);

/// <summary>订单项快照，包含当时的 SKU、商品名称、规格及价格；ReviewStatus 是当前评价状态。</summary>
public sealed record OrderItemDto(
    Guid Id,
    string Sku,
    string ProductName,
    string VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal,
    string? ReviewStatus
);

/// <summary>订单地址快照，客户之后修改地址簿不会改变此响应中的地址。</summary>
public sealed record OrderAddressDto(
    string AddressType,
    string RecipientName,
    string PostalCode,
    string CountryCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string? Phone
);

/// <summary>一次状态变更；初始记录 FromStatus 可以为空。</summary>
public sealed record OrderHistoryDto(
    string? FromStatus,
    string ToStatus,
    string? Reason,
    DateTime CreatedAt
);

/// <summary>订单下的一次支付尝试及其退款；失败重试可能让同一订单出现多条支付记录。</summary>
public sealed record OrderPaymentDto(
    Guid Id,
    string Provider,
    string Method,
    string Status,
    decimal Amount,
    string Currency,
    DateTime CreatedAt,
    DateTime? CapturedAt,
    IEnumerable<OrderRefundDto> Refunds
);

/// <summary>订单详情中的退款摘要，金额不能超过该笔支付的剩余可退额度。</summary>
public sealed record OrderRefundDto(
    Guid Id,
    decimal Amount,
    string Reason,
    string Status,
    DateTime CreatedAt
);

/// <summary>订单详情中的配送摘要；尚未发货或送达时对应时间允许为空。</summary>
public sealed record OrderShipmentDto(
    Guid Id,
    string Carrier,
    string? TrackingNumber,
    string Status,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    string Warehouse
);

/// <summary>订单列表摘要；PaymentStatus、ShipmentStatus 分别取最新记录的状态，不替代全部历史。</summary>
public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string Status,
    string Currency,
    decimal GrandTotal,
    DateTime PlacedAt,
    DateTime? PaidAt,
    string Customer,
    int ItemCount,
    string? FirstProductName,
    string? PaymentStatus,
    string? ShipmentStatus
);
