using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Inventory;

namespace MiniStore.Features.Products;

/// <summary>处理商品功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class ProductService(StoreDbContext db)
{
    /// <summary>按页面 slug 读取上架且未软删除的商品，用于商城详情页。</summary>
    public async Task<ProductDetailDto> GetBySlugAsync(string slug, CancellationToken ct)
    {
        return await DetailAsync(
            db.Products.Where(p => p.Slug == slug && p.Status == "active" && p.DeletedAt == null),
            ct
        );
    }

    /// <summary>按公开 UUID 读取可售商品；与 slug 入口应用相同的可见性限制。</summary>
    public async Task<ProductDetailDto> GetStoreAsync(Guid id, CancellationToken ct)
    {
        return await DetailAsync(
            db.Products.Where(p => p.PublicId == id && p.Status == "active" && p.DeletedAt == null),
            ct
        );
    }

    /// <summary>后台读取未软删除商品，允许查看草稿和下架状态；调用端点必须已通过后台授权。</summary>
    public async Task<ProductDetailDto> GetAdminAsync(Guid id, CancellationToken ct)
    {
        return await DetailAsync(
            db.Products.Where(p => p.PublicId == id && p.DeletedAt == null),
            ct
        );
    }

    /// <summary>创建商品、规格和每个仓库的零库存记录；SaveChanges 将这些新增关联一起保存。</summary>
    public async Task<ProductDetailDto> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        var product = new Product { CreatedAt = DateTime.UtcNow };
        db.Products.Add(product);
        await SaveAsync(product, request, ct);
        return await DetailAsync(db.Products.Where(p => p.Id == product.Id), ct);
    }

    /// <summary>加载已有规格与分类后更新商品；客户端必须提交 version，过期版本返回冲突。</summary>
    public async Task<ProductDetailDto> UpdateAsync(
        Guid id,
        ProductRequest request,
        CancellationToken ct
    )
    {
        var product =
            await db
                .Products.Include(p => p.ProductVariants)
                .Include(p => p.Categories)
                .SingleOrDefaultAsync(p => p.PublicId == id && p.DeletedAt == null, ct)
            ?? throw ApiError.NotFound();
        Rules.Require(request.Version.HasValue, "编辑商品必须提交 version。");
        db.Entry(product).Property<uint>("Version").OriginalValue = request.Version!.Value;
        await SaveAsync(product, request, ct);
        return await DetailAsync(db.Products.Where(p => p.Id == product.Id), ct);
    }

    /// <summary>按客户端版本更新上下架状态；首次上架时补充发布时间，不覆盖已存在的发布时间。</summary>
    public async Task SetStatusAsync(Guid id, ProductStatusRequest request, CancellationToken ct)
    {
        ValidateStatus(request.Status);
        var product =
            await db.Products.SingleOrDefaultAsync(p => p.PublicId == id && p.DeletedAt == null, ct)
            ?? throw ApiError.NotFound();
        db.Entry(product).Property<uint>("Version").OriginalValue = request.Version;
        product.Status = request.Status;
        product.UpdatedAt = DateTime.UtcNow;
        if (request.Status == "active")
            product.PublishedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

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

    /// <summary>只读获取品牌公开标识、名称和 slug，供筛选框使用。</summary>
    public async Task<List<NamedDto>> BrandsAsync(CancellationToken ct) =>
        await db
            .Brands.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new NamedDto(x.PublicId, x.Name, x.Slug))
            .ToListAsync(ct);

    /// <summary>一次读取小规模分类数据，再按 parent_id 在内存组织成树；商品数据不在此处加载。</summary>
    public async Task<List<CategoryNode>> CategoriesAsync(CancellationToken ct)
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

    /// <summary>组合筛选和稳定排序后在数据库分页；admin 由端点指定，不能由查询参数提升权限。</summary>
    public async Task<TableModel<ProductSummaryDto>> ListAsync(
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
            var pattern = Rules.ContainsPattern(q.Q);
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
        var ordered = q.Sort switch
        {
            null or "recommended" or "newest" => source
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id),
            "price_asc" => SortByPrice(source, false),
            "price_desc" => SortByPrice(source, true),
            "rating" => source
                .OrderByDescending(p =>
                    p.ProductReviews.Where(r => r.Status == "published")
                        .Average(r => (double?)r.Rating)
                    ?? 0
                )
                .ThenBy(p => p.PublicId),
            _ => throw new ApiError(
                400,
                "sort",
                "排序只支持 recommended、newest、price_asc、price_desc、rating。"
            ),
        };
        return await ordered.Select(Summary).PageAsync(q, ct);
    }

    // 先在 SQL 中按含税价排序，再投影为 DTO；构造 record 后按其属性排序可能无法翻译。
    // 这里的金额公式与 Summary 一致，食品与非食品价格都按前端实际显示值比较。
    /// <summary>按前端显示的含税价排序，同价时用公开 UUID 确定顺序；不会把所有商品读入内存。</summary>
    private static IQueryable<Product> SortByPrice(IQueryable<Product> source, bool descending)
    {
        var prices = source.Select(p => new
        {
            Product = p,
            Amount = Math.Round(
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
        });
        return (
            descending
                ? prices.OrderByDescending(p => p.Amount).ThenBy(p => p.Product.PublicId)
                : prices.OrderBy(p => p.Amount).ThenBy(p => p.Product.PublicId)
        ).Select(p => p.Product);
    }

    /// <summary>显式加载详情所需关系，并投影成 DTO；AsSplitQuery 避免多个集合 JOIN 造成大量重复行。</summary>
    private static async Task<ProductDetailDto> DetailAsync(
        IQueryable<Product> source,
        CancellationToken ct
    )
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
        var rate = IsFood(p) ? .08m : .10m;
        return new ProductDetailDto(
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
            p.Categories.Select(c => new NamedDto(c.PublicId, c.Name, c.Slug)),
            p.ProductVariants.OrderBy(v => v.Id)
                .Select(v => new ProductVariantDto(
                    v.PublicId,
                    v.Sku,
                    v.Name,
                    v.Price,
                    decimal.Round(v.Price * (1 + rate), 0, MidpointRounding.AwayFromZero),
                    v.Currency,
                    JsonSerializer.Deserialize<JsonElement>(v.Attributes),
                    v.WeightGrams,
                    v.IsActive,
                    v.Stocks.Sum(s => s.QuantityOnHand - s.QuantityReserved)
                )),
            p.ProductImages.OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(i.Url, i.AltText, i.IsPrimary))
        );
    }

    // ponytail: 教学库以固定 Food 分类识别食品；正式税务分类变化时改为独立税率字段或规则。
    /// <summary>根据已加载的 Food 分类判断教学用食品税率；只检查内存导航集合，不主动查询数据库。</summary>
    public static bool IsFood(Product p) =>
        p.Categories.Any(c =>
            c.Name == "Food" || c.Name.StartsWith("Food /", StringComparison.Ordinal)
        );

    /// <summary>校验允许写入的商品状态；数据库 CHECK 约束提供第二层防线。</summary>
    private static void ValidateStatus(string status) =>
        Rules.Require(status is "draft" or "active" or "inactive" or "archived", "商品状态无效。");

    /// <summary>校验并填充商品与规格，保留已有规格以保护历史订单，最后一次保存关联变更。</summary>
    private async Task SaveAsync(Product p, ProductRequest r, CancellationToken ct)
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
