using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 后台人员账户：与商城客户身份分离；运营人员可写，只读人员只能查询。
/// </summary>
public partial class AdminUser
{
    /// <summary>
    /// 管理员内部自增主键。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 管理员对外 UUID 标识。
    /// </summary>
    public Guid PublicId { get; set; }

    /// <summary>
    /// 登录邮箱，不区分大小写唯一。
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// ASP.NET Core PasswordHasher 生成的加盐密码哈希，不保存明文。
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// 后台人员显示名称。
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// 角色：operator 运营人员；viewer 只读人员。
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// 是否允许登录及访问后台。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 管理员账户创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
