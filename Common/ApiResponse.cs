namespace MiniStore.Common;

/// <summary>所有业务接口共用的响应外壳；只使用 Msg，不提供 message 别名。</summary>
/// <param name="Success">操作是否成功。</param>
/// <param name="Code">与实际 HTTP 状态一致的数字，例如 200、201、400。</param>
/// <param name="Msg">供界面展示的结果说明；错误不包含数据库内部信息。</param>
/// <param name="Data">成功时的业务数据；无返回值或失败时为 null。</param>
public sealed record ApiResponse<T>(bool Success, int Code, string Msg, T? Data);

/// <summary>只在 HTTP 端点组装响应；Service 仍返回业务 DTO，不依赖 HTTP 外壳。</summary>
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, int code = 200) => new(true, code, "Success", data);

    public static ApiResponse<object?> Ok() => new(true, 200, "Success", null);

    /// <summary>等待查询完成后包装具名 DTO，保留泛型类型供 OpenAPI 生成准确模型。</summary>
    public static async Task<ApiResponse<T>> OkAsync<T>(Task<T> task) => Ok(await task);

    public static ApiResponse<object?> Error(int code, string msg) => new(false, code, msg, null);
}

/// <summary>公司分页契约中的排序字段；当前排序由各查询固定，Orders 保留为空数组。</summary>
/// <param name="Asc">是否升序。</param>
/// <param name="Column">排序字段名。</param>
public sealed record TableOrder(bool Asc, string Column);

/// <summary>与公司 TableModel 对齐的分页结构；EF Core 执行计数和 Skip/Take。</summary>
/// <param name="Records">本页记录；空页返回空数组。</param>
/// <param name="Current">当前页码，从 1 开始；请求仍使用 page。</param>
/// <param name="Size">每页条数；请求仍使用 pageSize。</param>
/// <param name="Total">符合筛选条件的总记录数。</param>
public sealed record TableModel<T>(IReadOnlyList<T> Records, int Current, int Size, int Total)
{
    /// <summary>兼容字段；本项目没有自定义计数查询标识。</summary>
    public string CountId => "";

    /// <summary>本服务实际允许的每页上限，沿用现有 100 条限制。</summary>
    public int MaxLimit => 100;

    /// <summary>兼容公司契约；不是 EF Core 或 MyBatis 的运行开关。</summary>
    public bool OptimizeCountSql => true;

    /// <summary>兼容字段；实际排序见端点说明。</summary>
    public IReadOnlyList<TableOrder> Orders => [];

    /// <summary>总页数，向上取整；没有记录时为 0。</summary>
    public int Pages => Size > 0 ? (int)(((long)Total + Size - 1) / Size) : 0;

    /// <summary>本项目每次分页均执行总数查询。</summary>
    public bool SearchCount => true;
}
