using System.Text.Json;

namespace MiniStore.Features.Products;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>金额与币种；商品列表返回含税展示金额，IncludesTax 明确是否已经含税。</summary>
public sealed record MoneyDto(decimal Amount, string Currency, bool IncludesTax);

/// <summary>品牌或分类选择项，Id 是对外 UUID，Slug 是可读路径标识。</summary>
public sealed record NamedDto(Guid Id, string Name, string Slug);

/// <summary>商品列表卡片数据；价格取活动规格最低价，无活动规格时回退基础价，再按分类计税。</summary>
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

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Id</c>：编辑已有规格时传 UUID；新增规格传 null。</remarks>
/// <remarks><c>Price</c>：税前整数日元单价，不能由二进制浮点数表示金额。</remarks>
/// <remarks><c>Attributes</c>：JSON 对象，仅放颜色等规格属性；核心关系仍由外键维护。</remarks>
/// <param name="Id">更新现有规格时为其公开 UUID；新增规格传 null。</param>
/// <param name="Sku">规格唯一编码，必填，最多 64 字符，保存为大写。</param>
/// <param name="Name">名称，必填，长度限制见对应业务校验。</param>
/// <param name="Price">税前单价，非负整数日元，不是含税价。</param>
/// <param name="IsActive">是否启用。</param>
/// <param name="WeightGrams">重量 0～1000000，单位克。</param>
/// <param name="Attributes">JSON 对象，只放颜色等规格属性，原始 JSON 文本最多 4000 字符。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record VariantRequest(
    Guid? Id,
    string Sku,
    string Name,
    decimal Price,
    bool IsActive,
    int WeightGrams,
    JsonElement Attributes
);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Version</c>：编辑时必须提交读到的 xmin 版本；创建时可为空。</remarks>
/// <remarks><c>Variants</c>：提交完整规格集合，已有规格不可直接删除，可通过 IsActive 停用。</remarks>
/// <param name="Name">名称，必填，长度限制见对应业务校验。</param>
/// <param name="Slug">商品 URL 标识，小写字母、数字和连字符，最多 240 字符。</param>
/// <param name="BrandId">品牌公开 UUID。</param>
/// <param name="Description">商品描述，不可为 null，可为空字符串，最多 10000 字符。</param>
/// <param name="Status">商品状态：draft、active、inactive、archived。</param>
/// <param name="CategoryIds">活动分类公开 UUID 数组，1～10 项。</param>
/// <param name="Variants">完整规格数组，1～50 项；现有规格不能直接移除，可以停用。</param>
/// <param name="Version">编辑时必须提交最近读取的 xmin 版本；创建时可为空。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
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

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>Version</c>：上次读取的商品版本；不匹配时返回 409，防止覆盖并发修改。</remarks>
/// <param name="Status">目标商品状态：draft、active、inactive、archived。</param>
/// <param name="Version">最近读取的商品 xmin 版本；不匹配返回 409。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record ProductStatusRequest(string Status, uint Version);

/// <summary>分类树节点；Children 保存直接子节点，叶子节点的集合为空。</summary>
public sealed record CategoryNode(Guid Id, string Name, string Slug, List<CategoryNode> Children);

/// <summary>商品详情，组合展示信息、分类、规格与图片；不直接序列化 Product 实体。</summary>
public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string Slug,
    NamedDto Brand,
    string Status,
    MoneyDto Price,
    string? ThumbnailUrl,
    string StockStatus,
    double? Rating,
    int ReviewCount,
    uint Version,
    string? Description,
    IEnumerable<NamedDto> Categories,
    IEnumerable<ProductVariantDto> Variants,
    IEnumerable<ProductImageDto> Images
);

/// <summary>详情中的规格：Price 为税前单价，DisplayPrice 为含税单价，AvailableQuantity 为跨仓可售量。</summary>
public sealed record ProductVariantDto(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    decimal DisplayPrice,
    string Currency,
    JsonElement Attributes,
    int WeightGrams,
    bool IsActive,
    int AvailableQuantity
);

/// <summary>商品图片展示信息；主图标记和排序由服务查询决定。</summary>
public sealed record ProductImageDto(string Url, string AltText, bool IsPrimary);
