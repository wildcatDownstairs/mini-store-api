using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 树形商品分类：通过父分类关联支持多层目录。
/// </summary>
public partial class Category
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
    /// 父分类内部主键，关联 catalog.categories.id；为空表示根分类，不允许形成循环。
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 分类显示名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 用于页面地址或 API 查询的可读唯一标识。
    /// </summary>
    public string Slug { get; set; } = null!;

    /// <summary>
    /// 显示排序值，通常按升序展示。
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 分类是否启用，控制目录展示。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 记录创建时间，使用带时区时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
