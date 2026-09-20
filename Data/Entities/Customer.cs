using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 客户账户：保存客户身份、联系信息和账户状态；删除优先使用软删除。
/// </summary>
public partial class Customer
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
    /// 客户邮箱；通过 lower(email) 唯一索引保证大小写不敏感的唯一性，软删除后仍保留唯一约束。
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// 密码哈希；实验数据为不可用于登录的假 Argon2 风格字符串，禁止保存明文密码。
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// 客户名字，不含姓氏。
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// 客户姓氏。
    /// </summary>
    public string LastName { get; set; } = null!;

    /// <summary>
    /// 联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 出生日期，仅保存日期，不含时分秒。
    /// </summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>
    /// 账户状态：active 正常、disabled 停用、pending 待激活。
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 邮箱验证通过时间；为空表示尚未验证。
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>
    /// 最近一次登录时间；为空表示没有登录记录。
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 软删除时间；为空表示未软删除。
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>客户的历史购物车；部分唯一索引只限制一个 active 购物车。</summary>
    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CheckoutRequest> CheckoutRequests { get; set; } =
        new List<CheckoutRequest>();

    public virtual ICollection<CouponRedemption> CouponRedemptions { get; set; } =
        new List<CouponRedemption>();

    public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } =
        new List<CustomerAddress>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } =
        new List<ProductReview>();
}
