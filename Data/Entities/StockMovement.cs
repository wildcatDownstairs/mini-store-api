using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 库存变动流水：进货、销售、退货、调整影响实物库存，预占和释放影响预占库存。
/// </summary>
public partial class StockMovement
{
    /// <summary>
    /// 内部主键，使用 bigint 自增标识列，供表间关联使用。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 仓库内部主键，关联 inventory.warehouses.id。
    /// </summary>
    public long WarehouseId { get; set; }

    /// <summary>
    /// 商品变体内部主键，关联 catalog.product_variants.id。
    /// </summary>
    public long VariantId { get; set; }

    /// <summary>
    /// 变动类型：purchase 进货、sale 销售出库、return 退货入库、adjustment 库存调整、reservation 预占、release 释放预占。
    /// </summary>
    public string MovementType { get; set; } = null!;

    /// <summary>
    /// 本次变动数量，不得为零；进货、退货、预占为正，销售、释放为负，调整可正可负；预占和释放仅改变预占库存。
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 来源类型：order 订单、purchase 采购、adjustment 库存调整。
    /// </summary>
    public string ReferenceType { get; set; } = null!;

    /// <summary>
    /// 来源记录标识；来源为订单时必须填写订单内部主键；采购及调整尚未建立对应业务表。
    /// </summary>
    public long? ReferenceId { get; set; }

    /// <summary>
    /// 补充说明或操作备注。
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// 库存变动发生时间，按此时间及流水主键重建库存余额。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 生成列：来源类型为 order 时取 reference_id，否则为空；外键校验来源订单存在，不可手动写入。
    /// </summary>
    public long? OrderId { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Stock Stock { get; set; } = null!;
}
