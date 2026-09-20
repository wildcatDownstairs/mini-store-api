using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName
);

public sealed record TokenResponse(
    string AccessToken,
    DateTime ExpiresAt,
    Guid UserId,
    string DisplayName,
    string Role
);

public static class AuthEndpoints
{
    public static void AddStoreAuth(this WebApplicationBuilder builder)
    {
        var key = builder.Configuration["Auth:SigningKey"];
        if (key is null || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException(
                "请用 scripts/run_api.py 启动，或配置至少 32 字节的 Auth__SigningKey；禁止源码默认密钥。"
            );
        builder
            .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidIssuer = "mini-store",
                    ValidateAudience = true,
                    ValidAudience = "mini-store-clients",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.FromSeconds(5),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var db =
                            context.HttpContext.RequestServices.GetRequiredService<StoreDbContext>();
                        var principal = context.Principal!;
                        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var id))
                        {
                            context.Fail("身份无效");
                            return;
                        }
                        long internalId;
                        string role;
                        if (principal.FindFirstValue("kind") == "admin")
                        {
                            var admin = await db
                                .AdminUsers.AsNoTracking()
                                .SingleOrDefaultAsync(
                                    x => x.PublicId == id && x.IsActive,
                                    context.HttpContext.RequestAborted
                                );
                            if (admin is null)
                            {
                                context.Fail("账户不可用");
                                return;
                            }
                            internalId = admin.Id;
                            role = admin.Role;
                        }
                        else
                        {
                            var customer = await db
                                .Customers.AsNoTracking()
                                .SingleOrDefaultAsync(
                                    x =>
                                        x.PublicId == id
                                        && x.Status == "active"
                                        && x.DeletedAt == null,
                                    context.HttpContext.RequestAborted
                                );
                            if (customer is null)
                            {
                                context.Fail("账户不可用");
                                return;
                            }
                            internalId = customer.Id;
                            role = "customer";
                        }
                        // 每次请求查询当前账户状态；停用后旧 Token 也不能继续使用。内部主键不写进签发的 Token。
                        var identity = (ClaimsIdentity)principal.Identity!;
                        foreach (var claim in identity.FindAll("role").ToList())
                            identity.RemoveClaim(claim);
                        identity.AddClaim(new("role", role));
                        identity.AddClaim(new("db_id", internalId.ToString()));
                    },
                };
            });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "Customer",
                p => p.RequireAuthenticatedUser().RequireRole("customer")
            );
            options.AddPolicy(
                "AdminRead",
                p =>
                    p.RequireAuthenticatedUser()
                        .RequireClaim("kind", "admin")
                        .RequireRole("operator", "viewer")
            );
            options.AddPolicy(
                "AdminWrite",
                p =>
                    p.RequireAuthenticatedUser()
                        .RequireClaim("kind", "admin")
                        .RequireRole("operator")
            );
        });
    }

    public static void MapAuth(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("身份认证").RequireRateLimiting("auth");
        group
            .MapPost(
                "/register",
                async (
                    RegisterRequest request,
                    StoreDbContext db,
                    IConfiguration config,
                    CancellationToken ct
                ) =>
                {
                    var email = Email(request.Email);
                    Password(request.Password);
                    var now = DateTime.UtcNow;
                    var customer = new Customer
                    {
                        Email = email,
                        FirstName = Rules.Text(request.FirstName, 80, "名字"),
                        LastName = Rules.Text(request.LastName, 80, "姓氏"),
                        Status = "active",
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    customer.PasswordHash = new PasswordHasher<Customer>().HashPassword(
                        customer,
                        request.Password
                    );
                    db.Customers.Add(customer);
                    await db.SaveChangesAsync(ct);
                    // 本地学习版不发送验证邮件；email_verified_at 仍为空，不伪造已验证状态。
                    return Results.Ok(
                        Issue(
                            customer.PublicId,
                            customer.LastName + " " + customer.FirstName,
                            "customer",
                            "customer",
                            config
                        )
                    );
                }
            )
            .WithSummary("注册客户并获取短期访问令牌");
        group
            .MapPost(
                "/login",
                async (
                    LoginRequest request,
                    StoreDbContext db,
                    IConfiguration config,
                    CancellationToken ct
                ) =>
                {
                    var email = Email(request.Email);
                    var customer = await db.Customers.SingleOrDefaultAsync(
                        x => x.Email.ToLower() == email,
                        ct
                    );
                    if (
                        customer is null
                        || customer.Status != "active"
                        || customer.DeletedAt != null
                        || !Verify(customer.PasswordHash, request.Password)
                    )
                        throw new ApiError(
                            401,
                            "invalid_credentials",
                            "邮箱或密码错误，或账户不可用；种子客户假密码不可登录。"
                        );
                    customer.LastLoginAt = DateTime.UtcNow;
                    customer.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return Issue(
                        customer.PublicId,
                        customer.LastName + " " + customer.FirstName,
                        "customer",
                        "customer",
                        config
                    );
                }
            )
            .WithSummary("客户登录");
        app.MapPost(
                "/api/admin/auth/login",
                async (
                    LoginRequest request,
                    StoreDbContext db,
                    IConfiguration config,
                    CancellationToken ct
                ) =>
                {
                    var email = Email(request.Email);
                    var admin = await db
                        .AdminUsers.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Email.ToLower() == email, ct);
                    if (
                        admin is null
                        || !admin.IsActive
                        || !Verify(admin.PasswordHash, request.Password)
                    )
                        throw new ApiError(
                            401,
                            "invalid_credentials",
                            "管理员邮箱或密码错误，或账户不可用。"
                        );
                    return Issue(admin.PublicId, admin.DisplayName, admin.Role, "admin", config);
                }
            )
            .RequireRateLimiting("auth")
            .WithTags("身份认证")
            .WithSummary("独立后台账户登录");
    }

    public static string Email(string? input)
    {
        var email = Rules.Text(input, 254, "邮箱").ToLowerInvariant();
        Rules.Require(
            MailAddress.TryCreate(email, out var parsed)
                && parsed.Address == email
                && email.Contains('@'),
            "邮箱格式不正确。"
        );
        return email;
    }

    public static void Password(string? value) =>
        Rules.Require(value is { Length: >= 12 and <= 128 }, "密码长度须为 12～128 字符。");

    private static bool Verify(string hash, string? password)
    {
        if (password is null || password.Length > 128 || !hash.StartsWith("AQAAAA"))
            return false;
        try
        {
            return new PasswordHasher<object>().VerifyHashedPassword(new(), hash, password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static TokenResponse Issue(
        Guid id,
        string name,
        string role,
        string kind,
        IConfiguration config
    )
    {
        var expires = DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(
            "mini-store",
            "mini-store-clients",
            [
                new("sub", id.ToString()),
                new("role", role),
                new("kind", kind),
                new("jti", Guid.NewGuid().ToString()),
            ],
            expires: expires,
            signingCredentials: new(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256
            )
        );
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, id, name, role);
    }

    /// <summary>显式命令创建后台人员；密码来自环境，不提供固定默认密码，也不覆盖已有账号。</summary>
    public static async Task CreateAdminAsync(StoreDbContext db, CancellationToken ct)
    {
        var email = Email(Environment.GetEnvironmentVariable("ADMIN_EMAIL"));
        var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        Password(password);
        var role = Environment.GetEnvironmentVariable("ADMIN_ROLE") ?? "operator";
        Rules.Require(role is "operator" or "viewer", "ADMIN_ROLE 只能为 operator 或 viewer。");
        var admin = new AdminUser
        {
            Email = email,
            DisplayName = "本地学习管理员",
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        admin.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(admin, password!);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);
        Console.WriteLine("后台账号已创建；密码未写入源码或日志。");
    }
}
