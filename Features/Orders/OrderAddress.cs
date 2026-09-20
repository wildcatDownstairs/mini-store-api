namespace MiniStore.Features.Orders;

/// <summary>
/// 订单地址快照：保存下单时收货及账单地址，不依赖客户当前地址簿。
/// </summary>
public partial class OrderAddress
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 订单内部主键，关联 sales.orders.id。
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 下单时的地址快照：地址类型：shipping 表示收货地址，billing 表示账单地址。
    /// </summary>
    public string AddressType { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：收件人或账单接收人姓名。
    /// </summary>
    public string RecipientName { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：邮政编码；日本地址通常采用三位数字加连字符加四位数字。
    /// </summary>
    public string PostalCode { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：两位国家或地区代码，例如 JP 表示日本。
    /// </summary>
    public string CountryCode { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：都道府县名称，例如东京都或大阪府。
    /// </summary>
    public string Prefecture { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：市、区、町或村名称。
    /// </summary>
    public string City { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：详细地址第一行，通常包含町名、丁目及门牌号。
    /// </summary>
    public string AddressLine1 { get; set; } = null!;

    /// <summary>
    /// 下单时的地址快照：详细地址第二行，通常包含楼名和房间号，可为空。
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// 下单时的地址快照：联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>关联的订单导航属性；与外键 ID 不同，它表示关联对象。</summary>
    public virtual Order Order { get; set; } = null!;
}
