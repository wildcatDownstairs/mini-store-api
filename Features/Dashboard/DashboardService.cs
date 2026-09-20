using Microsoft.EntityFrameworkCore;
using MiniStore.Data;

namespace MiniStore.Features.Dashboard;

/// <summary>处理运营概览功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class DashboardService(StoreDbContext db)
{
    /// <summary>汇总最近 30 天 JPY 已付款订单的 GMV、客单价和趋势，同时读取全量状态分布及当前待办。</summary>
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var paid = db.Orders.AsNoTracking().Where(o => o.PaidAt >= since && o.Currency == "JPY");
        var count = await paid.CountAsync(ct);
        var revenue = await paid.SumAsync(o => o.GrandTotal, ct);
        // GMV 含税、运费且不扣退款；退款单独展示，避免把成交额误当成净收入。
        var refunds = await db
            .Refunds.Where(r =>
                r.Status == "completed" && r.CompletedAt >= since && r.Payment.Currency == "JPY"
            )
            .SumAsync(r => r.Amount, ct);
        var daily = await paid.GroupBy(o => o.PaidAt!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailySalesDto(g.Key, g.Sum(o => o.GrandTotal), g.Count()))
            .ToListAsync(ct);
        var statuses = await db
            .Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusCountDto(g.Key, g.Count()))
            .ToListAsync(ct);
        var topProducts = await db
            .OrderItems.AsNoTracking()
            .Where(i => i.Order.PaidAt >= since && i.Order.Currency == "JPY")
            .GroupBy(i => new
            {
                i.Product.PublicId,
                i.Product.Name,
                Brand = i.Product.Brand.Name,
            })
            .OrderByDescending(g => g.Sum(i => i.Quantity))
            .ThenBy(g => g.Key.PublicId)
            .Take(5)
            .Select(g => new TopProductDto(
                g.Key.PublicId,
                g.Key.Name,
                g.Key.Brand,
                g.Sum(i => i.Quantity)
            ))
            .ToListAsync(ct);
        return new DashboardDto(
            topProducts,
            "JPY",
            30,
            revenue,
            count,
            count == 0 ? 0 : decimal.Round(revenue / count, 0),
            refunds,
            await db.Products.CountAsync(p => p.Status == "active" && p.DeletedAt == null, ct),
            await db.Customers.CountAsync(ct),
            await db.Stocks.CountAsync(
                s => s.QuantityOnHand - s.QuantityReserved < s.ReorderLevel,
                ct
            ),
            await db.Refunds.CountAsync(r => r.Status == "pending", ct),
            await db.ProductReviews.CountAsync(r => r.Status == "pending", ct),
            daily,
            statuses
        );
    }
}
