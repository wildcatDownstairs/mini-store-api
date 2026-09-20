using MiniStore.Common;

namespace MiniStore.Features.Products;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 ProductService。</summary>
public static class ProductEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapProducts(this WebApplication app)
    {
        var store = app.MapGroup("/api/store").WithTags("商城商品");
        var admin = app.MapGroup("/api/admin")
            .WithTags("商品管理")
            .RequireAuthorization("AdminRead");

        store
            .MapGet(
                "/products",
                ([AsParameters] ListQuery q, ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.ListAsync(q, false, ct))
            )
            .WithName("ListStoreProducts")
            .WithSummary("搜索筛选上架商品")
            .WithDescription(
                "公开接口。支持 page、pageSize、q、category、brand、inStock、sort；category 为分类 slug 并包含子分类，brand 为公开 UUID。只展示 active 且未删除商品；价格含税。sort 支持 recommended/newest/price_asc/price_desc/rating，默认按创建时间倒序。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "/products",
                ([AsParameters] ListQuery q, ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.ListAsync(q, true, ct))
            )
            .WithName("ListAdminProducts")
            .WithSummary("分页查询后台商品")
            .WithDescription(
                "需要 operator 或 viewer。支持商城列表筛选项及 status，能读取未删除的草稿、下架商品。返回 version 供后续编辑并发校验；价格字段按 DTO 区分含税展示价与税前维护价。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        store
            .MapGet(
                "/products/{slug}",
                (string slug, ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.GetBySlugAsync(slug, ct))
            )
            .WithName("GetProductBySlug")
            .WithSummary("通过 slug 读取商品详情")
            .WithDescription(
                "公开接口。仅返回 active 且未删除商品、规格、图片和库存；slug 是 URL 友好字符串，不是商品 UUID。不可见或不存在返回 404。"
            )
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        store
            .MapGet(
                "/products/by-id/{id:guid}",
                (Guid id, ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.GetStoreAsync(id, ct))
            )
            .WithName("GetStoreProduct")
            .WithSummary("通过 UUID 读取商品详情")
            .WithDescription(
                "公开接口。id 是商品 public_id，只返回上架且未删除商品；详情包含规格、含税展示价格、评分和可售量。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "/products/{id:guid}",
                (Guid id, ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.GetAdminAsync(id, ct))
            )
            .WithName("GetAdminProduct")
            .WithSummary("读取后台商品详情")
            .WithDescription(
                "需要 operator 或 viewer。id 为商品公开 UUID，可查看未删除的草稿和下架商品；version 用于后续写入时避免覆盖并发修改。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        store
            .MapGet(
                "/brands",
                (ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.BrandsAsync(ct))
            )
            .WithName("ListStoreBrands")
            .WithSummary("列出商城品牌")
            .WithDescription(
                "公开接口。返回品牌公开 UUID、名称与 slug，按名称排序，供商品筛选使用。"
            )
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "/brands",
                (ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.BrandsAsync(ct))
            )
            .WithName("ListAdminBrands")
            .WithSummary("列出后台品牌选项")
            .WithDescription(
                "需要 operator 或 viewer。返回品牌公开 UUID、名称与 slug；当前接口仅查询，不提供品牌写入。"
            )
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        store
            .MapGet(
                "/categories",
                (ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.CategoriesAsync(ct))
            )
            .WithName("ListStoreCategories")
            .WithSummary("读取商城分类树")
            .WithDescription(
                "公开接口。按 parent_id 组织活动分类为树，children 为空数组表示叶子；商品列表的 category 参数使用节点 slug。"
            )
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "/categories",
                (ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.CategoriesAsync(ct))
            )
            .WithName("ListAdminCategories")
            .WithSummary("读取后台活动分类树")
            .WithDescription(
                "需要 operator 或 viewer。返回活动分类的树形选项；当前接口不包含停用分类，也不提供分类写入。"
            )
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPost(
                "/products",
                async (ProductRequest request, ProductService service, CancellationToken ct) =>
                {
                    var result = await service.CreateAsync(request, ct);
                    return TypedResults.Created(
                        $"/api/admin/products/{result.Id}",
                        ApiResponse.Ok(result, 201)
                    );
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("CreateProduct")
            .WithSummary("创建商品和规格")
            .WithDescription(
                "仅 operator。brandId/categoryIds 为公开 UUID；规格单价是税前整数 JPY，SKU 唯一。商品、规格和各仓零库存一起保存；返回 201、商品详情与 Location，新增库存需另行调整。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPut(
                "/products/{id:guid}",
                (Guid id, ProductRequest request, ProductService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.UpdateAsync(id, request, ct))
            )
            .RequireAuthorization("AdminWrite")
            .WithName("UpdateProduct")
            .WithSummary("编辑商品并校验版本")
            .WithDescription(
                "仅 operator。必须提交当前 version；过期版本返回 409。现有规格不可从数组直接删除，可以停用；新增规格在各仓创建零库存。响应 200 为更新后的详情，历史订单快照不变。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPatch(
                "/products/{id:guid}/status",
                async (
                    Guid id,
                    ProductStatusRequest request,
                    ProductService service,
                    CancellationToken ct
                ) =>
                {
                    await service.SetStatusAsync(id, request, ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("SetProductStatus")
            .WithSummary("修改商品状态")
            .WithDescription(
                "仅 operator。接受 draft/active/inactive/archived 及当前 version；冲突返回 409。首次 active 时补发布时间，成功返回 200，data 为 null。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);
    }
}
