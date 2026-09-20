using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Catalog;

public sealed record MoneyDto(decimal Amount, string Currency, bool IncludesTax);

public sealed record NamedDto(Guid Id, string Name, string Slug);

public sealed record ProductSummaryDto(
    Guid Id,
    string Slug,
    string Name,
    NamedDto Brand,
    string Status,
    MoneyDto Price,
    string? ThumbnailUrl,
    string StockStatus,
    double? Rating,
    int ReviewCount,
    uint Version,
    string? Sku,
    int VariantCount,
    int AvailableQuantity,
    string? Category
);

public sealed record VariantRequest(
    Guid? Id,
    string Sku,
    string Name,
    decimal Price,
    bool IsActive,
    int WeightGrams,
    JsonElement Attributes
);

public sealed record ProductRequest(
    string Name,
    string Slug,
    Guid BrandId,
    string Description,
    string Status,
    Guid[] CategoryIds,
    VariantRequest[] Variants,
    uint? Version
);

public sealed record ProductStatusRequest(string Status, uint Version);

public sealed record CategoryNode(Guid Id, string Name, string Slug, List<CategoryNode> Children);

public static class CatalogEndpoints
{
    // 投影在数据库端运行，仅提取前端需要的字段；不会把实体图或内部主键序列化出去。
    private static readonly Expression<Func<Product, ProductSummaryDto>> Summary = p =>
        new(
            p.PublicId,
            p.Slug,
            p.Name,
            new(p.Brand.PublicId, p.Brand.Name, p.Brand.Slug),
            p.Status,
            new(
                Math.Round(
                    (
                        p.ProductVariants.Where(v => v.IsActive).Min(v => (decimal?)v.Price)
                        ?? p.BasePrice
                    )
                        * (
                            p.Categories.Any(c => c.Name == "Food" || c.Name.StartsWith("Food /"))
                                ? 1.08m
                                : 1.10m
                        ),
                    0
                ),
                p.Currency,
                true
            ),
            p.ProductImages.OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.Url)
                .FirstOrDefault(),
            p.ProductVariants.Any(v =>
                v.IsActive && v.Stocks.Any(s => s.QuantityOnHand > s.QuantityReserved)
            )
                ? "in_stock"
                : "out_of_stock",
            p.ProductReviews.Where(r => r.Status == "published").Average(r => (double?)r.Rating),
            p.ProductReviews.Count(r => r.Status == "published"),
            EF.Property<uint>(p, "Version"),
            p.ProductVariants.OrderBy(v => v.Id).Select(v => v.Sku).FirstOrDefault(),
            p.ProductVariants.Count,
            p.ProductVariants.Where(v => v.IsActive)
                .SelectMany(v => v.Stocks)
                .Sum(s => s.QuantityOnHand - s.QuantityReserved),
            p.Categories.OrderBy(c => c.Id).Select(c => c.Name).FirstOrDefault()
        );

    public static void MapCatalog(this WebApplication app)
    {
        var store = app.MapGroup("/api/store").WithTags("商城商品");
        var admin = app.MapGroup("/api/admin")
            .WithTags("商品管理")
            .RequireAuthorization("AdminRead");
        store
            .MapGet(
                "/products",
                (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
                    Products(db, q, false, ct)
            )
            .WithSummary("搜索、筛选并分页浏览上架商品");
        admin.MapGet(
            "/products",
            (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
                Products(db, q, true, ct)
        );
        store.MapGet(
            "/products/{slug}",
            (string slug, StoreDbContext db, CancellationToken ct) =>
                Detail(
                    db.Products.Where(p =>
                        p.Slug == slug && p.Status == "active" && p.DeletedAt == null
                    ),
                    ct
                )
        );
        store.MapGet(
            "/products/by-id/{id:guid}",
            (Guid id, StoreDbContext db, CancellationToken ct) =>
                Detail(
                    db.Products.Where(p =>
                        p.PublicId == id && p.Status == "active" && p.DeletedAt == null
                    ),
                    ct
                )
        );
        admin.MapGet(
            "/products/{id:guid}",
            (Guid id, StoreDbContext db, CancellationToken ct) =>
                Detail(db.Products.Where(p => p.PublicId == id && p.DeletedAt == null), ct)
        );
        store.MapGet("/brands", Brands);
        admin.MapGet("/brands", Brands);
        store.MapGet("/categories", Categories);
        admin.MapGet("/categories", Categories);
        admin
            .MapPost(
                "/products",
                async (ProductRequest request, StoreDbContext db, CancellationToken ct) =>
                {
                    var product = new Product { CreatedAt = DateTime.UtcNow };
                    db.Products.Add(product);
                    await Save(product, request, db, ct);
                    return Results.Created(
                        $"/api/admin/products/{product.PublicId}",
                        await Detail(db.Products.Where(p => p.Id == product.Id), ct)
                    );
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithSummary("创建商品、规格与零库存记录");
        admin
            .MapPut(
                "/products/{id:guid}",
                async (Guid id, ProductRequest request, StoreDbContext db, CancellationToken ct) =>
                {
                    var product =
                        await db
                            .Products.Include(p => p.ProductVariants)
                            .Include(p => p.Categories)
                            .SingleOrDefaultAsync(p => p.PublicId == id && p.DeletedAt == null, ct)
                        ?? throw ApiError.NotFound();
                    Rules.Require(request.Version.HasValue, "编辑商品必须提交 version。");
                    db.Entry(product).Property<uint>("Version").OriginalValue = request
                        .Version!
                        .Value;
                    await Save(product, request, db, ct);
                    return await Detail(db.Products.Where(p => p.Id == product.Id), ct);
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithSummary("编辑商品；version 防止覆盖并发修改");
        admin
            .MapPatch(
                "/products/{id:guid}/status",
                async (
                    Guid id,
                    ProductStatusRequest request,
                    StoreDbContext db,
                    CancellationToken ct
                ) =>
                {
                    ValidateStatus(request.Status);
                    var product =
                        await db.Products.SingleOrDefaultAsync(
                            p => p.PublicId == id && p.DeletedAt == null,
                            ct
                        ) ?? throw ApiError.NotFound();
                    db.Entry(product).Property<uint>("Version").OriginalValue = request.Version;
                    product.Status = request.Status;
                    product.UpdatedAt = DateTime.UtcNow;
                    if (request.Status == "active")
                        product.PublishedAt ??= DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
    }

    private static async Task<object> Brands(StoreDbContext db, CancellationToken ct) =>
        await db
            .Brands.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new NamedDto(x.PublicId, x.Name, x.Slug))
            .ToListAsync(ct);

    private static async Task<List<CategoryNode>> Categories(
        StoreDbContext db,
        CancellationToken ct
    )
    {
        var all = await db
            .Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
        List<CategoryNode> Children(long? parent) =>
            all.Where(c => c.ParentId == parent)
                .Select(c => new CategoryNode(c.PublicId, c.Name, c.Slug, Children(c.Id)))
                .ToList();
        return Children(null);
    }

    private static async Task<PageResult<ProductSummaryDto>> Products(
        StoreDbContext db,
        ListQuery q,
        bool admin,
        CancellationToken ct
    )
    {
        q.Validate();
        var source = db.Products.AsNoTracking().Where(p => p.DeletedAt == null);
        if (!admin)
            source = source.Where(p => p.Status == "active");
        if (!string.IsNullOrWhiteSpace(q.Status) && admin)
        {
            ValidateStatus(q.Status);
            source = source.Where(p => p.Status == q.Status);
        }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var pattern = "%" + q.Q.Trim() + "%";
            source = source.Where(p =>
                EF.Functions.ILike(p.Name, pattern)
                || EF.Functions.ILike(p.Brand.Name, pattern)
                || p.ProductVariants.Any(v => EF.Functions.ILike(v.Sku, pattern))
            );
        }
        if (q.Brand is { } brand)
            source = source.Where(p => p.Brand.PublicId == brand);
        if (q.Category is { Length: > 0 } category)
        {
            // 分类规模有限，先在内存展开整棵树；商品仍在 SQL 中筛选和分页。
            var categories = await db
                .Categories.AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.ParentId,
                    c.Slug,
                })
                .ToListAsync(ct);
            var root = categories.FirstOrDefault(c => c.Slug == category);
            var ids = new HashSet<long>();
            if (root != null)
            {
                ids.Add(root.Id);
                bool changed;
                do
                {
                    changed = false;
                    foreach (var c in categories)
                        if (c.ParentId.HasValue && ids.Contains(c.ParentId.Value))
                            changed |= ids.Add(c.Id);
                } while (changed);
            }
            source = source.Where(p => p.Categories.Any(c => ids.Contains(c.Id)));
        }
        if (q.InStock)
            source = source.Where(p =>
                p.ProductVariants.Any(v =>
                    v.IsActive && v.Stocks.Any(s => s.QuantityOnHand > s.QuantityReserved)
                )
            );
        var rows = source.Select(Summary);
        var ordered = q.Sort switch
        {
            null or "recommended" or "newest" => source
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Select(Summary),
            "price_asc" => rows.OrderBy(p => p.Price.Amount).ThenBy(p => p.Id),
            "price_desc" => rows.OrderByDescending(p => p.Price.Amount).ThenBy(p => p.Id),
            "rating" => rows.OrderByDescending(p => p.Rating ?? 0).ThenBy(p => p.Id),
            _ => throw new ApiError(
                400,
                "sort",
                "排序只支持 recommended、newest、price_asc、price_desc、rating。"
            ),
        };
        return await ordered.PageAsync(q, ct);
    }

    private static async Task<object> Detail(IQueryable<Product> source, CancellationToken ct)
    {
        var p =
            await source
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Brand)
                .Include(x => x.Categories)
                .Include(x => x.ProductImages)
                .Include(x => x.ProductVariants)
                    .ThenInclude(x => x.Stocks)
                .SingleOrDefaultAsync(ct)
            ?? throw ApiError.NotFound();
        var summary = await source.AsNoTracking().Select(Summary).SingleAsync(ct);
        var rate = Food(p) ? .08m : .10m;
        return new
        {
            summary.Id,
            summary.Name,
            summary.Slug,
            summary.Brand,
            summary.Status,
            summary.Price,
            summary.ThumbnailUrl,
            summary.StockStatus,
            summary.Rating,
            summary.ReviewCount,
            summary.Version,
            p.Description,
            categories = p.Categories.Select(c => new NamedDto(c.PublicId, c.Name, c.Slug)),
            variants = p
                .ProductVariants.OrderBy(v => v.Id)
                .Select(v => new
                {
                    id = v.PublicId,
                    v.Sku,
                    v.Name,
                    v.Price,
                    displayPrice = decimal.Round(
                        v.Price * (1 + rate),
                        0,
                        MidpointRounding.AwayFromZero
                    ),
                    v.Currency,
                    attributes = JsonSerializer.Deserialize<JsonElement>(v.Attributes),
                    v.WeightGrams,
                    v.IsActive,
                    availableQuantity = v.Stocks.Sum(s => s.QuantityOnHand - s.QuantityReserved),
                }),
            images = p
                .ProductImages.OrderBy(i => i.SortOrder)
                .Select(i => new
                {
                    i.Url,
                    i.AltText,
                    i.IsPrimary,
                }),
        };
    }

    // ponytail: 教学库以固定 Food 分类识别食品；正式税务分类变化时改为独立税率字段或规则。
    public static bool Food(Product p) =>
        p.Categories.Any(c =>
            c.Name == "Food" || c.Name.StartsWith("Food /", StringComparison.Ordinal)
        );

    private static void ValidateStatus(string status) =>
        Rules.Require(status is "draft" or "active" or "inactive" or "archived", "商品状态无效。");

    private static async Task Save(
        Product p,
        ProductRequest r,
        StoreDbContext db,
        CancellationToken ct
    )
    {
        ValidateStatus(r.Status);
        p.Name = Rules.Text(r.Name, 200, "商品名");
        p.Slug = Rules.Text(r.Slug, 240, "slug");
        Rules.Require(
            Regex.IsMatch(p.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"),
            "slug 只能使用小写字母、数字和连字符。"
        );
        Rules.Require(r.Description is { Length: <= 10000 }, "商品介绍不能超过一万字。");
        p.Description = r.Description;
        p.BrandId = (
            await db.Brands.SingleOrDefaultAsync(b => b.PublicId == r.BrandId, ct)
            ?? throw new ApiError(400, "brand", "品牌不存在。")
        ).Id;
        Rules.Require(
            r.CategoryIds is { Length: > 0 and <= 10 } && r.Variants is { Length: > 0 and <= 50 },
            "请选择 1～10 个分类和 1～50 个规格。"
        );
        var categories = await db
            .Categories.Where(c => r.CategoryIds.Contains(c.PublicId) && c.IsActive)
            .ToListAsync(ct);
        Rules.Require(categories.Count == r.CategoryIds.Distinct().Count(), "分类不存在或未启用。");
        p.Categories = categories;
        var previous = p.ProductVariants.ToList();
        Rules.Require(
            previous.All(v => r.Variants.Any(n => n.Id == v.PublicId)),
            "已有规格不能移除，请将其停用以保留订单和库存关联。"
        );
        Rules.Require(
            r.Variants.Where(v => v.Id.HasValue).Select(v => v.Id).Distinct().Count()
                == r.Variants.Count(v => v.Id.HasValue),
            "规格 ID 重复。"
        );
        Rules.Require(
            r.Variants.Select(v => v.Sku?.Trim().ToUpperInvariant()).Distinct().Count()
                == r.Variants.Length,
            "SKU 重复。"
        );
        var warehouses = await db.Warehouses.Select(w => w.Id).ToListAsync(ct);
        foreach (var input in r.Variants)
        {
            Rules.Money(input.Price, "单价");
            Rules.Require(
                input.WeightGrams is >= 0 and <= 1000000
                    && input.Attributes.ValueKind == JsonValueKind.Object
                    && input.Attributes.GetRawText().Length <= 4000,
                "重量或规格属性无效。"
            );
            var variant = input.Id.HasValue
                ? previous.SingleOrDefault(v => v.PublicId == input.Id)
                    ?? throw new ApiError(400, "variant", "规格不属于该商品。")
                : new ProductVariant { CreatedAt = DateTime.UtcNow };
            variant.Sku = Rules.Text(input.Sku, 64, "SKU").ToUpperInvariant();
            variant.Name = Rules.Text(input.Name, 120, "规格名");
            variant.Price = input.Price;
            variant.IsActive = input.IsActive;
            variant.Currency = "JPY";
            variant.Attributes = input.Attributes.GetRawText();
            variant.WeightGrams = input.WeightGrams;
            variant.UpdatedAt = DateTime.UtcNow;
            if (!input.Id.HasValue)
            {
                p.ProductVariants.Add(variant);
                foreach (var warehouse in warehouses)
                    variant.Stocks.Add(
                        new Stock
                        {
                            WarehouseId = warehouse,
                            ReorderLevel = 10,
                            UpdatedAt = DateTime.UtcNow,
                        }
                    );
            }
        }
        p.BasePrice = r.Variants.Min(v => v.Price);
        p.Currency = "JPY";
        p.Status = r.Status;
        p.UpdatedAt = DateTime.UtcNow;
        if (r.Status == "active")
            p.PublishedAt ??= DateTime.UtcNow;
        // 一次 SaveChanges 自带事务；商品、分类关系、新规格与零库存必须一起成功。
        await db.SaveChangesAsync(ct);
    }
}
