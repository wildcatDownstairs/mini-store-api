using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 客户购物车：每位客户最多一个使用中的购物车。
/// </summary>
public partial class Cart
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
    /// 所属客户的内部主键，关联 account.customers.id。
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// 购物车状态：active 使用中、converted 已转订单、abandoned 已放弃。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 购物车转为订单的时间；仅 converted 状态非空。
    /// </summary>
    public DateTime? CheckedOutAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Customer Customer { get; set; } = null!;
}
