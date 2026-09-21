using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace MiniStore.Common;

// 构造参数的默认值会被 Minimal API 当作可选查询参数；属性初始化器不会。
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 的 AsParameters 从查询字符串创建分页参数。"
)]
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
    /// <summary>限制分页大小与筛选文本长度，防止一次请求无界读取或处理过长输入。</summary>
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

/// <summary>多个功能共用的输入校验、身份读取与分页扩展方法。</summary>
public static class Rules
{
    /// <summary>业务前置条件不满足时抛出统一的 400 错误；数据库约束仍必须保留。</summary>
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new ApiError(400, "validation", message);
    }

    /// <summary>验证必填文本和去除首尾空白后的长度，返回整理后的文本。</summary>
    public static string Text(string? value, int max, string label)
    {
        Require(
            !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max,
            $"{label}必填且不能超过 {max} 字。"
        );
        return value!.Trim();
    }

    /// <summary>
    /// 生成 PostgreSQL ILIKE 的包含匹配。先转义 \、% 和 _，再包上两侧百分号。
    /// 默认转义符是反斜杠，因此用户输入按字面量搜索，不会变成通配符。
    /// </summary>
    public static string ContainsPattern(string value)
    {
        var literal = value
            .Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return "%" + literal + "%";
    }

    /// <summary>当前业务只接受范围内的整数日元；NUMERIC 的精度能力不等于 API 允许任意小数。</summary>
    public static void Money(decimal value, string label, bool positive = false) =>
        Require(
            value >= (positive ? 1 : 0) && value <= 999999999 && value == decimal.Truncate(value),
            $"{label}须为范围内的整数日元。"
        );

    /// <summary>读取认证中间件补入的内部 bigint 标识；仅能在已通过认证的端点调用。</summary>
    public static long ActorId(this ClaimsPrincipal user) =>
        long.Parse(user.FindFirstValue("db_id")!);

    /// <summary>读取令牌 sub 中的公开 UUID；仅能在已通过认证的端点调用。</summary>
    public static Guid PublicId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub")!);

    /// <summary>先计数，再在 SQL 中执行 OFFSET/LIMIT；调用方需预先提供稳定排序，两次查询不是同一快照。</summary>
    public static async Task<TableModel<T>> PageAsync<T>(
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
