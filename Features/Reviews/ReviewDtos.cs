namespace MiniStore.Features.Reviews;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Rating</c>：1～5 星；是否已验证购买由服务端按订单关系确定。</remarks>
/// <param name="Rating">评分，1～5。</param>
/// <param name="Title">可空评价标题，最多 160 字符。</param>
/// <param name="Content">可空评价正文，最多 1000 字符。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record ReviewRequest(short Rating, string? Title, string? Content);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Status</c>：只允许 published 发布或 rejected 拒绝；只能处理 pending 评价。</remarks>
/// <param name="Status">审核结果：published 或 rejected；只允许处理 pending 评价。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record ReviewDecision(string Status);

/// <summary>商城公开评价，不暴露客户邮箱；Author 使用姓氏加称呼。</summary>
public sealed record ProductReviewDto(
    Guid Id,
    string Author,
    short Rating,
    string? Title,
    string? Content,
    bool IsVerifiedPurchase,
    DateTime CreatedAt
);

/// <summary>客户提交评价后的公开标识及 pending 状态，需后台审核后才展示。</summary>
public sealed record ReviewCreatedDto(Guid Id, string Status);

/// <summary>后台评价摘要；无订单项关联的历史评价可能没有 OrderId 和 OrderNumber。</summary>
public sealed record ReviewSummaryDto(
    Guid Id,
    Guid ProductId,
    string Product,
    string Customer,
    Guid? OrderId,
    string? OrderNumber,
    short Rating,
    string? Title,
    string? Content,
    bool IsVerifiedPurchase,
    string Status,
    DateTime CreatedAt
);
