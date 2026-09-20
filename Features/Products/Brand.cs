namespace MiniStore.Features.Products;

/// <summary>
/// 商品品牌：保存品牌名称、访问标识和所属国家。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "EF Core 查询会自动创建实体或视图结果。"
)]
public partial class Brand
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。
    /// </summary>
    public Guid PublicId { get; set; }

    /// <summary>
    /// 品牌显示名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 用于页面地址或 API 查询的可读唯一标识。
    /// </summary>
    public string Slug { get; set; } = null!;

    /// <summary>
    /// 品牌所属国家或地区的两位代码，例如 JP。
    /// </summary>
    public string CountryCode { get; set; } = null!;

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>关联的商品集合；需通过 Include 或投影显式加载。</summary>
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
