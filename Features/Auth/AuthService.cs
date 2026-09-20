using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Customers;

namespace MiniStore.Features.Auth;

/// <summary>处理身份认证功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class AuthService(StoreDbContext db, IConfiguration config)
{
    /// <summary>注册客户、哈希保存密码并签发令牌；邮箱重复由数据库唯一索引兜底。</summary>
    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
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
        return Issue(
            customer.PublicId,
            customer.LastName + " " + customer.FirstName,
            "customer",
            "customer",
            config
        );
    }

    /// <summary>校验客户状态和密码，更新登录时间后签发访问令牌；种子假哈希不能用于登录。</summary>
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Email(request.Email);
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Email.ToLower() == email, ct);
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

    /// <summary>仅查询独立的后台账号表；客户令牌不能借此获得管理员身份。</summary>
    public async Task<TokenResponse> LoginAdminAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Email(request.Email);
        var admin = await db
            .AdminUsers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Email.ToLower() == email, ct);
        if (admin is null || !admin.IsActive || !Verify(admin.PasswordHash, request.Password))
            throw new ApiError(401, "invalid_credentials", "管理员邮箱或密码错误，或账户不可用。");
        return Issue(admin.PublicId, admin.DisplayName, admin.Role, "admin", config);
    }

    /// <summary>统一邮箱大小写并校验格式；数据库仍使用 lower(email) 唯一索引防止并发重复。</summary>
    private static string Email(string? input)
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

    /// <summary>校验输入密码长度；此方法不负责哈希，存储前由 PasswordHasher 处理。</summary>
    private static void Password(string? value) =>
        Rules.Require(value is { Length: >= 12 and <= 128 }, "密码长度须为 12～128 字符。");

    /// <summary>验证 ASP.NET Identity 格式的密码哈希；哈希不可逆，不能解密还原密码。</summary>
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

    /// <summary>签发 30 分钟有效的 JWT，写入公开 UUID 与角色；令牌中不保存密码或内部主键。</summary>
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
