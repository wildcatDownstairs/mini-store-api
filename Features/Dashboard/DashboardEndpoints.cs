using Microsoft.EntityFrameworkCore;
using MiniStore.Data;

namespace MiniStore.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboard(this WebApplication app)
    {
        app.MapGet(
                "/api/admin/dashboard",
                async (StoreDbContext db, CancellationToken ct) =>
                {
                    var since = DateTime.UtcNow.AddDays(-30);
                    var paid = db
                        .Orders.AsNoTracking()
                        .Where(o => o.PaidAt >= since && o.Currency == "JPY");
                    var count = await paid.CountAsync(ct);
                    var revenue = await paid.SumAsync(o => o.GrandTotal, ct);
                    // GMV 含税、运费且不扣退款；退款单独展示，避免把成交额误当成净收入。
                    var refunds = await db
                        .Refunds.Where(r =>
                            r.Status == "completed"
                            && r.CompletedAt >= since
                            && r.Payment.Currency == "JPY"
                        )
                        .SumAsync(r => r.Amount, ct);
                    var daily = await paid.GroupBy(o => o.PaidAt!.Value.Date)
                        .Select(g => new
                        {
                            day = g.Key,
                            amount = g.Sum(o => o.GrandTotal),
                            orders = g.Count(),
                        })
                        .OrderBy(x => x.day)
                        .ToListAsync(ct);
                    var statuses = await db
                        .Orders.AsNoTracking()
                        .GroupBy(o => o.Status)
                        .Select(g => new { status = g.Key, count = g.Count() })
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
                        .Select(g => new
                        {
                            id = g.Key.PublicId,
                            name = g.Key.Name,
                            brand = g.Key.Brand,
                            sales = g.Sum(i => i.Quantity),
                        })
                        .OrderByDescending(p => p.sales)
                        .ThenBy(p => p.id)
                        .Take(5)
                        .ToListAsync(ct);
                    return new
                    {
                        topProducts,
                        currency = "JPY",
                        periodDays = 30,
                        gmv = revenue,
                        paidOrders = count,
                        aov = count == 0 ? 0 : decimal.Round(revenue / count, 0),
                        refunded = refunds,
                        activeProducts = await db.Products.CountAsync(
                            p => p.Status == "active" && p.DeletedAt == null,
                            ct
                        ),
                        customers = await db.Customers.CountAsync(ct),
                        lowStocks = await db.Stocks.CountAsync(
                            s => s.QuantityOnHand - s.QuantityReserved < s.ReorderLevel,
                            ct
                        ),
                        pendingRefunds = await db.Refunds.CountAsync(
                            r => r.Status == "pending",
                            ct
                        ),
                        pendingReviews = await db.ProductReviews.CountAsync(
                            r => r.Status == "pending",
                            ct
                        ),
                        daily,
                        statuses,
                    };
                }
            )
            .RequireAuthorization("AdminRead")
            .WithTags("运营概览")
            .WithSummary("最近 30 天成交指标与运营待办");
    }
}
