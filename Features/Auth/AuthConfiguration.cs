using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniStore.Data;

namespace MiniStore.Features.Auth;

/// <summary>配置令牌认证和访问策略；每次请求核对账户当前状态。</summary>
public static class AuthConfiguration
{
    /// <summary>注册 JWT 认证及客户、只读后台、可写后台三种授权策略。认证回答“是谁”，授权回答“能做什么”。</summary>
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
}
