namespace MiniStore.Features.Auth;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <param name="Email">登录邮箱，不区分大小写，最多 254 字符。</param>
/// <param name="Password">登录密码，不在响应中返回；凭据错误返回 401。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record LoginRequest(string Email, string Password);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <param name="Email">登录邮箱，不区分大小写，最多 254 字符。</param>
/// <param name="Password">密码，12～128 字符；只通过安全连接发送，不写入日志。</param>
/// <param name="FirstName">名，必填，最多 80 字符。</param>
/// <param name="LastName">姓，必填，最多 80 字符。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName
);

/// <summary>登录凭证与显示信息；AccessToken 用于 Authorization: Bearer 请求头，到期后需重新登录。</summary>
public sealed record TokenResponse(
    string AccessToken,
    DateTime ExpiresAt,
    Guid UserId,
    string DisplayName,
    string Role
);
