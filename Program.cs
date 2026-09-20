using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Auth;
using MiniStore.Features.Carts;
using MiniStore.Features.Checkout;
using MiniStore.Features.Customers;
using MiniStore.Features.Dashboard;
using MiniStore.Features.Inventory;
using MiniStore.Features.Marketing;
using MiniStore.Features.Orders;
using MiniStore.Features.Payments;
using MiniStore.Features.Products;
using MiniStore.Features.Reviews;
using MiniStore.Features.Shipping;
using Scalar.AspNetCore;

// Program 只负责组装服务和路由，具体业务放在各个 Feature 文件夹。
var builder = WebApplication.CreateBuilder(args);

// AddDbContext 默认注册为 Scoped：一次请求共用一个上下文，用来跟踪实体修改并协调事务。
// UseNpgsql 指定 PostgreSQL 驱动；这里只配置连接，并不会创建、重置或填充数据库。
builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseNpgsql(DatabaseSettings.Connection(builder.Configuration))
);
if (args.Contains("--create-admin"))
{
    // 管理命令无需启动 HTTP 服务器，也无需配置访问令牌密钥。
    using var commandApp = builder.Build();
    using var scope = commandApp.Services.CreateScope();
    await AuthService.CreateAdminAsync(
        scope.ServiceProvider.GetRequiredService<StoreDbContext>(),
        CancellationToken.None
    );
    return;
}
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 128 * 1024);

// 异常和无响应体的 HTTP 错误都使用公司响应结构；仍保留真实 HTTP 状态。
// 异常中间件需要兜底服务才能启动；实际异常均由下方处理器输出 ApiResponse。
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// 内部调用 AddOpenApi，统一生成中文说明、请求响应模型以及 JWT 文档声明。
builder.Services.AddStoreOpenApi();

// Data Protection 用于保护结算报价令牌；登录 JWT 使用 Auth 中的另一套签名配置。
builder.Services.AddDataProtection();
builder.AddStoreAuth();

// Service 构造参数由容器自动提供。它们依赖 Scoped DbContext，不能注册成跨请求共享的单例。
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<MarketingService>();
builder.Services.AddScoped<ShippingService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<AuthService>();

// CORS 允许两个本地前端来源读取 API 响应；它不是登录校验，也不能替代角色和数据归属检查。
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                    ??
                    [
                        "http://localhost:5173",
                        "http://127.0.0.1:5173",
                        "http://localhost:5174",
                        "http://127.0.0.1:5174",
                    ]
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
    )
);

// 此策略仅由声明 RequireRateLimiting("auth") 的登录、注册端点使用，按来源 IP 限制请求频率。
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy(
        "auth",
        http =>
            RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ =>
                    new()
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }
            )
    );
});
var app = builder.Build();

// 中间件按顺序包住请求：先安排统一错误处理，再认证“是谁”，最后授权“可否访问”。
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    await context.HttpContext.Response.WriteAsJsonAsync(
        ApiResponse.Error(
            status,
            status switch
            {
                400 => "请求格式或参数不正确。",
                401 => "请重新登录。",
                403 => "没有执行此操作的权限。",
                404 => "请求的资源不存在。",
                405 => "该接口不支持此请求方法。",
                429 => "操作太频繁，请稍后再试。",
                503 => "数据库暂时不可用。",
                _ => "请求失败，请稍后重试。",
            }
        )
    );
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet(
        "/health",
        async (StoreDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(ApiResponse.Ok(new HealthResponse("healthy")))
                : Results.StatusCode(503)
    )
    .Produces<ApiResponse<HealthResponse>>()
    .WithName("GetHealth")
    .WithSummary("检查服务与数据库连接")
    .WithDescription(
        "公开接口。数据库可连接返回 200 和 status=healthy，不可连接返回 503；不返回连接串或密码。"
    )
    .WithTags("系统")
    .Produces<ApiResponse<object?>>(500)
    .Produces<ApiResponse<object?>>(503);
app.MapGet(
        "/api/config",
        (IWebHostEnvironment env, IConfiguration config) =>
            ApiResponse.Ok(
                new PublicConfigurationResponse(
                    env.IsDevelopment() && config.GetValue<bool>("Features:SimulatedPayments"),
                    "JPY"
                )
            )
    )
    .WithName("GetPublicConfiguration")
    .WithSummary("读取前端公开配置")
    .WithDescription(
        "公开接口。返回 JPY 币种及当前是否允许模拟支付；模拟开关只有 Development 且显式启用才为 true，不包含服务器密钥。"
    )
    .WithTags("系统")
    .Produces<ApiResponse<object?>>(500);
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Scalar 读取同一份内置 OpenAPI 文档，无需为 UI 再注册一套文档生成服务。
    // 仅在开发环境开放；Authentication 中填入登录返回的 JWT，不需手工加 Bearer 前缀。
    app.MapScalarApiReference(options =>
        options
            .WithTitle("Mini Store 电商接口文档")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .DisableDefaultFonts()
            .DisableAgent()
    );
}

// 这些 Map 方法只注册路由，不会在启动时执行下单或查询商品等业务。
app.MapAuth();
app.MapProducts();
app.MapCustomers();
app.MapCart();
app.MapCheckout();
app.MapOrders();
app.MapInventory();
app.MapPayments();
app.MapShipping();
app.MapMarketing();
app.MapReviews();
app.MapDashboard();
app.Run();
