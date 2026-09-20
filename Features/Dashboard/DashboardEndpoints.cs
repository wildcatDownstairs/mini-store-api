namespace MiniStore.Features.Dashboard;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 DashboardService。</summary>
public static class DashboardEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapDashboard(this WebApplication app)
    {
        app.MapGet(
                "/api/admin/dashboard",
                (DashboardService service, CancellationToken ct) => service.GetAsync(ct)
            )
            .RequireAuthorization("AdminRead")
            .WithTags("运营概览")
            .WithName("GetDashboard")
            .WithSummary("读取近 30 天运营概览")
            .WithDescription(
                "需要 operator 或 viewer。返回 JPY 成交额、订单数、客单价、退款、每日走势与热销商品；状态统计和待办使用各自口径，不全部受 30 天限制。无成交日期不补零，跨查询不保证同一数据快照。"
            )
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
    }
}
