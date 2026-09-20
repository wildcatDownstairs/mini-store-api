using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MiniStore.Common;

/// <summary>可预期的业务错误；与程序缺陷区分开，给前端稳定的 HTTP 状态和错误代码。</summary>
public sealed class ApiError(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static ApiError NotFound() => new(404, "not_found", "记录不存在，或你无权访问。");

    public static ApiError Conflict(string message) => new(409, "conflict", message);
}

/// <summary>统一异常出口，不把 SQL、密码哈希或数据库内部错误发送给浏览器。</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception error,
        CancellationToken ct
    )
    {
        var pg = error as PostgresException ?? error.InnerException as PostgresException;
        var (status, _, detail) = error switch
        {
            ApiError a => (a.Status, a.Code, a.Message),
            DbUpdateConcurrencyException => (
                409,
                "stale_version",
                "记录已被他人修改，请刷新后再试。"
            ),
            BadHttpRequestException => (400, "invalid_request", "请求格式或参数不正确。"),
            _ when pg?.SqlState is "23505" => (409, "duplicate", "邮箱、编码或业务记录已存在。"),
            _ when pg?.SqlState is "23503" or "23514" or "23502" or "22001" or "22003" => (
                400,
                "constraint",
                "数据不符合数据库约束，请检查输入。"
            ),
            _ when pg?.SqlState is "P0001" or "40001" or "40P01" or "55P03" => (
                409,
                "transaction_conflict",
                "库存、退款额度或记录状态已变化，请刷新后重试。"
            ),
            _ => (500, "internal_error", "服务器暂时无法完成操作。"),
        };
        if (status == 500)
            logger.LogError(error, "未处理异常，追踪号 {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(ApiResponse.Error(status, detail), ct);
        return true;
    }
}
