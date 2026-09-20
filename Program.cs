using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Auth;
using MiniStore.Features.Cart;
using MiniStore.Features.Catalog;
using MiniStore.Features.Checkout;
using MiniStore.Features.Customers;
using MiniStore.Features.Dashboard;
using MiniStore.Features.Inventory;
using MiniStore.Features.Marketing;
using MiniStore.Features.Orders;
using MiniStore.Features.Payments;
using MiniStore.Features.Reviews;
using MiniStore.Features.Shipping;

// Program 只负责组装服务和路由，具体业务放在各个 Feature 文件夹。
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseNpgsql(DatabaseSettings.Connection(builder.Configuration))
);
if (args.Contains("--create-admin"))
{
    // 管理命令无需启动 HTTP 服务器，也无需配置访问令牌密钥。
    using var commandApp = builder.Build();
    using var scope = commandApp.Services.CreateScope();
    await AuthEndpoints.CreateAdminAsync(
        scope.ServiceProvider.GetRequiredService<StoreDbContext>(),
        CancellationToken.None
    );
    return;
}
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 128 * 1024);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.AddStoreAuth();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<OrderService>();
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
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    await Results
        .Problem(
            statusCode: status,
            title: status switch
            {
                401 => "unauthorized",
                403 => "forbidden",
                404 => "not_found",
                429 => "too_many_requests",
                _ => "request_failed",
            }
        )
        .ExecuteAsync(context.HttpContext);
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet(
    "/health",
    async (StoreDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "healthy" })
            : Results.StatusCode(503)
);
app.MapGet(
    "/api/config",
    (IWebHostEnvironment env, IConfiguration config) =>
        new
        {
            simulatedPayments = env.IsDevelopment()
                && config.GetValue<bool>("Features:SimulatedPayments"),
            currency = "JPY",
        }
);
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapAuth();
app.MapCatalog();
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
