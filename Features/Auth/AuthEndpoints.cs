namespace MiniStore.Features.Auth;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 AuthService。</summary>
public static class AuthEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapAuth(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("身份认证").RequireRateLimiting("auth");
        group
            .MapPost(
                "/register",
                (RegisterRequest request, AuthService service, CancellationToken ct) =>
                    service.RegisterAsync(request, ct)
            )
            .WithName("RegisterCustomer")
            .WithSummary("注册客户并签发访问令牌")
            .WithDescription(
                "公开接口；邮箱不区分大小写，密码须为 12～128 字符。创建 active 客户并返回 30 分钟 JWT；不代表邮箱已验证。重复邮箱返回 409；按来源 IP 限流，超限返回 429。"
            )
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(429)
            .ProducesProblem(500);
        group
            .MapPost(
                "/login",
                (LoginRequest request, AuthService service, CancellationToken ct) =>
                    service.LoginAsync(request, ct)
            )
            .WithName("LoginCustomer")
            .WithSummary("客户登录")
            .WithDescription(
                "公开接口；仅验证客户账号，种子数据中的假哈希不能登录。邮箱或密码错误、账号不可用返回 401；成功返回 30 分钟 JWT。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(500);
        app.MapPost(
                "/api/admin/auth/login",
                (LoginRequest request, AuthService service, CancellationToken ct) =>
                    service.LoginAdminAsync(request, ct)
            )
            .RequireRateLimiting("auth")
            .WithTags("身份认证")
            .WithName("LoginAdministrator")
            .WithSummary("后台账号登录")
            .WithDescription(
                "公开接口；仅接受独立后台账号，不接受客户身份。成功返回 operator 或 viewer 角色的访问令牌；凭据错误或账号停用返回 401。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(500);
    }
}
