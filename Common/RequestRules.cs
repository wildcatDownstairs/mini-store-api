using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace MiniStore.Common;

/// <summary>record 适合表达只携带数据的请求或响应；泛型 T 使各种列表共用分页外壳。</summary>
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

// 构造参数的默认值会被 Minimal API 当作可选查询参数；属性初始化器不会。
public sealed record ListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Q = null,
    string? Status = null,
    string? Sort = null,
    string? Category = null,
    Guid? Brand = null,
    Guid? Warehouse = null,
    bool InStock = false,
    bool LowStock = false
)
{
    public void Validate()
    {
        Rules.Require(
            Page is > 0 and <= 10000 && PageSize is > 0 and <= 100,
            "分页范围错误：page 1～10000，pageSize 1～100。"
        );
        Rules.Require(
            Q?.Length is not > 100 && Status?.Length is not > 24 && Category?.Length is not > 160,
            "筛选条件过长。"
        );
    }
}

public static class Rules
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new ApiError(400, "validation", message);
    }

    public static string Text(string? value, int max, string label)
    {
        Require(
            !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max,
            $"{label}必填且不能超过 {max} 字。"
        );
        return value!.Trim();
    }

    public static void Money(decimal value, string label, bool positive = false) =>
        Require(
            value >= (positive ? 1 : 0) && value <= 999999999 && value == decimal.Truncate(value),
            $"{label}须为范围内的整数日元。"
        );

    public static long ActorId(this ClaimsPrincipal user) =>
        long.Parse(user.FindFirstValue("db_id")!);

    public static Guid PublicId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub")!);

    public static async Task<PageResult<T>> PageAsync<T>(
        this IQueryable<T> source,
        ListQuery query,
        CancellationToken ct
    )
    {
        query.Validate();
        // IQueryable 在 ToListAsync 等终结操作之前不会查库；Skip/Take 将成为数据库分页语句。
        var total = await source.CountAsync(ct);
        return new(
            await source
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct),
            query.Page,
            query.PageSize,
            total
        );
    }
}
