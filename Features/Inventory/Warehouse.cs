using MiniStore.Features.Shipping;

namespace MiniStore.Features.Inventory;

/// <summary>
/// 仓库资料：保存仓库编码、名称及日本仓库地址。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "EF Core 查询会自动创建实体或视图结果。"
)]
public partial class Warehouse
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
    /// 唯一仓库编码，供库存与发货业务引用。
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// 仓库显示名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 邮政编码；日本地址通常采用三位数字加连字符加四位数字。
    /// </summary>
    public string PostalCode { get; set; } = null!;

    /// <summary>
    /// 都道府县名称，例如东京都或大阪府。
    /// </summary>
    public string Prefecture { get; set; } = null!;

    /// <summary>
    /// 市、区、町或村名称。
    /// </summary>
    public string City { get; set; } = null!;

    /// <summary>
    /// 仓库详细地址。
    /// </summary>
    public string Address { get; set; } = null!;

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>关联的物流记录集合；需通过 Include 或投影显式加载。</summary>
    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    /// <summary>关联的库存记录集合；需通过 Include 或投影显式加载。</summary>
    public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
