namespace MiniStore.Features.Dashboard;

/// <summary>按已付款时间的日期汇总 JPY 成交金额和订单数；未补齐没有成交的日期。</summary>
public sealed record DailySalesDto(DateTime Day, decimal Amount, int Orders);

/// <summary>所有订单的状态分布，不受 Dashboard 最近 30 天成交窗口限制。</summary>
public sealed record OrderStatusCountDto(string Status, int Count);

/// <summary>最近 30 天已付款 JPY 订单按件数排名的商品；Name 与 Brand 为当前商品资料。</summary>
public sealed record TopProductDto(Guid Id, string Name, string Brand, int Sales);

/// <summary>运营概览：成交指标取最近 30 天，状态分布与待办取全量或当前值；GMV 不扣退款。</summary>
public sealed record DashboardDto(
    List<TopProductDto> TopProducts,
    string Currency,
    int PeriodDays,
    decimal Gmv,
    int PaidOrders,
    decimal Aov,
    decimal Refunded,
    int ActiveProducts,
    int Customers,
    int LowStocks,
    int PendingRefunds,
    int PendingReviews,
    List<DailySalesDto> Daily,
    List<OrderStatusCountDto> Statuses
);
