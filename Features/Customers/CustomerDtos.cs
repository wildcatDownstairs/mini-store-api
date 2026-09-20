namespace MiniStore.Features.Customers;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>IsDefault</c>：同一客户、同一地址类型只能有一个默认地址。</remarks>
/// <param name="AddressType">地址类型：shipping 收货地址或 billing 账单地址。</param>
/// <param name="RecipientName">收件人姓名，必填，最多 160 字符。</param>
/// <param name="PostalCode">日本邮编，7 位数字，允许中间连字符，例如 100-0001。</param>
/// <param name="CountryCode">当前接口只接受 JP。</param>
/// <param name="Prefecture">都道府县，必填，最多 80 字符。</param>
/// <param name="City">城市，必填，最多 80 字符。</param>
/// <param name="AddressLine1">街道门牌，必填，最多 160 字符。</param>
/// <param name="AddressLine2">楼名房号，可为空，最多 160 字符。</param>
/// <param name="Phone">联系电话，地址请求必填；资料请求可为空，具体格式由服务校验。</param>
/// <param name="IsDefault">是否作为该客户同类型的默认地址；服务端在事务中切换旧默认项。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record AddressRequest(
    string AddressType,
    string RecipientName,
    string PostalCode,
    string CountryCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string Phone,
    bool IsDefault
);

/// <summary>地址展示数据，Id 对应地址的 public_id，可用于编辑或结算。</summary>
public sealed record AddressDto(
    Guid Id,
    string AddressType,
    string RecipientName,
    string PostalCode,
    string CountryCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string? Phone,
    bool IsDefault
);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <param name="FirstName">名，必填，最多 80 字符。</param>
/// <param name="LastName">姓，必填，最多 80 字符。</param>
/// <param name="Phone">联系电话，地址请求必填；资料请求可为空，具体格式由服务校验。</param>
/// <param name="BirthDate">可空生日，格式 YYYY-MM-DD，不得晚于今天。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record ProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly? BirthDate
);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <param name="Status">客户状态，只允许 active 或 disabled。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record CustomerStatusRequest(string Status);

/// <summary>当前客户的个人资料响应，不包含密码哈希、内部主键或导航集合。</summary>
public sealed record CustomerProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly? BirthDate,
    string Status,
    DateTime CreatedAt
);

/// <summary>后台客户列表；TotalSpent 是已付款 JPY 订单金额之和，尚未扣除退款。</summary>
public sealed record CustomerSummaryDto(
    Guid Id,
    string Email,
    string Name,
    string? Phone,
    string Status,
    DateTime CreatedAt,
    int OrderCount,
    decimal TotalSpent
);

/// <summary>后台客户详情，包含地址及最近订单摘要，不是全部订单的无限列表。</summary>
public sealed record CustomerDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    DateTime CreatedAt,
    IEnumerable<AddressDto> Addresses,
    List<CustomerOrderDto> RecentOrders
);

/// <summary>客户详情中的近期订单摘要；详细快照从订单模块读取。</summary>
public sealed record CustomerOrderDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal GrandTotal,
    string Currency,
    DateTime PlacedAt
);
