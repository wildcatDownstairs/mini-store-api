using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace MiniStore.Common;

/// <summary>只配置 API 文档，不执行授权或业务校验；真正的权限仍由认证授权中间件负责。</summary>
public static class OpenApiDocumentation
{
    /// <summary>注册中文文档、JWT 安全声明和通用参数说明，具体业务说明写在各 Endpoints 中。</summary>
    public static void AddStoreOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(
                (document, context, ct) =>
                {
                    document.Info.Title = "Mini Store 电商 API";
                    document.Info.Version = "v1";
                    document.Info.Description =
                        "商城与管理后台共用的学习接口。公开标识使用 UUID；金额为 JPY，时间使用带时区的 ISO 8601。具体税前/含税语义见各 DTO。错误采用 ProblemDetails；模拟支付不产生真实资金流。";
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??=
                        new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description =
                            "先调用对应客户或后台登录接口。在下方仅粘贴 accessToken，UI 自动添加 Bearer 前缀；令牌有效期 30 分钟。",
                    };
                    return Task.CompletedTask;
                }
            );
            options.AddOperationTransformer(
                (operation, context, ct) =>
                {
                    // 从真实路由权限元数据推导，避免把公开登录接口错误标记为必须带令牌。
                    var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                    if (
                        metadata.OfType<IAuthorizeData>().Any()
                        && !metadata.OfType<IAllowAnonymous>().Any()
                    )
                    {
                        operation.Security =
                        [
                            new OpenApiSecurityRequirement
                            {
                                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] =
                                [],
                            },
                        ];
                    }
                    foreach (var parameter in operation.Parameters ?? [])
                    {
                        if (
                            parameter is not OpenApiParameter item
                            || !string.IsNullOrWhiteSpace(item.Description)
                        )
                            continue;
                        item.Description =
                            item.In == ParameterLocation.Path
                                ? item.Name == "slug"
                                    ? "商品的 URL 友好 slug。"
                                    : "路径资源的公开 UUID（不是数据库 bigint 主键）；具体资源见接口说明。"
                                : item.Name?.ToLowerInvariant() switch
                                {
                                    "page" => "页码，1～10000，默认 1；超过末页返回空 items。",
                                    "pagesize" => "每页条数，1～100，默认 20。",
                                    "q" => "关键词，最多 100 字；搜索哪些字段见本接口说明。",
                                    "status" =>
                                        "状态筛选，最多 24 字；本接口支持的状态及是否生效见接口说明。",
                                    "sort" =>
                                        "商品排序：recommended、newest、price_asc、price_desc、rating；其他列表不使用。",
                                    "category" =>
                                        "商品分类 slug（不是 UUID），包括子分类；仅商品列表使用，最多 160 字。",
                                    "brand" => "品牌公开 UUID；仅商品列表使用。",
                                    "warehouse" => "仓库公开 UUID；仅库存、流水和物流列表使用。",
                                    "instock" =>
                                        "是否仅显示有可售库存商品，默认 false；仅商品列表使用。",
                                    "lowstock" =>
                                        "是否仅显示可售量低于补货线的记录，默认 false；仅库存列表使用。",
                                    _ => "参数含义与允许值见接口说明。",
                                };
                    }
                    foreach (
                        var (status, response) in operation.Responses ?? new OpenApiResponses()
                    )
                    {
                        if (response is not OpenApiResponse item)
                            continue;
                        item.Description = status switch
                        {
                            "200" =>
                                "成功；返回 JSON。下单接口的 200 表示返回已成功的幂等请求结果。",
                            "201" => "首次创建成功；返回新资源信息。",
                            "204" => "操作成功，无响应体。",
                            "400" =>
                                "请求格式、参数或数据库约束不满足；读取 ProblemDetails 的 title/detail。",
                            "401" => "未登录、令牌无效或账号不可用；登录端点也可能表示凭据错误。",
                            "403" => "身份无权操作，或当前环境禁止模拟支付/退款。",
                            "404" => "记录不存在、不可见或不属于当前客户。",
                            "409" => "版本、状态、唯一性、幂等内容、库存或退款额度发生冲突。",
                            "422" => "优惠券未满足有效期、门槛或可用次数等业务条件。",
                            "429" => "注册或登录请求超过限流额度，请稍后重试。",
                            "500" =>
                                "服务内部错误；不返回 SQL 或凭据，异常响应含 traceId 用于查日志。",
                            "503" => "健康检查未能连接数据库。",
                            _ => item.Description,
                        };
                    }
                    return Task.CompletedTask;
                }
            );
        });
    }
}

/// <summary>健康检查成功响应；仅反映当前数据库是否可连接。</summary>
/// <param name="Status">连接正常时为 healthy。</param>
public sealed record HealthResponse(string Status);

/// <summary>可公开给前端的配置，不包含连接串或签名密钥。</summary>
/// <param name="SimulatedPayments">当前环境是否启用模拟支付，不表示已接入真实支付网关。</param>
/// <param name="Currency">当前业务币种 JPY。</param>
public sealed record PublicConfigurationResponse(bool SimulatedPayments, string Currency);
