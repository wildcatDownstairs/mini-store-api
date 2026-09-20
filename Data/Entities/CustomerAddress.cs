using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 客户当前地址簿：支持收货地址、账单地址及各类型默认地址；修改不会影响历史订单地址快照。
/// </summary>
public partial class CustomerAddress
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属客户的内部主键，关联 account.customers.id。
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// 地址类型：shipping 表示收货地址，billing 表示账单地址。
    /// </summary>
    public string AddressType { get; set; } = null!;

    /// <summary>
    /// 收件人或账单接收人姓名。
    /// </summary>
    public string RecipientName { get; set; } = null!;

    /// <summary>
    /// 邮政编码；日本地址通常采用三位数字加连字符加四位数字。
    /// </summary>
    public string PostalCode { get; set; } = null!;

    /// <summary>
    /// 两位国家或地区代码，例如 JP 表示日本。
    /// </summary>
    public string CountryCode { get; set; } = null!;

    /// <summary>
    /// 都道府县名称，例如东京都或大阪府。
    /// </summary>
    public string Prefecture { get; set; } = null!;

    /// <summary>
    /// 市、区、町或村名称。
    /// </summary>
    public string City { get; set; } = null!;

    /// <summary>
    /// 详细地址第一行，通常包含町名、丁目及门牌号。
    /// </summary>
    public string AddressLine1 { get; set; } = null!;

    /// <summary>
    /// 详细地址第二行，通常包含楼名和房间号，可为空。
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// 联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 是否为该客户在此地址类型下的默认地址；同一客户和地址类型最多一条默认地址。
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 地址对外 UUID 标识；用于 API 定位，仍须校验客户归属。
    /// </summary>
    public Guid PublicId { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
