using System;
using System.Collections.Generic;

namespace MiniStore.Data.Entities;

/// <summary>
/// 仓库与商品变体的当前库存；可售库存等于实物库存减预占库存。
/// </summary>
public partial class Stock
{
    /// <summary>
    /// 仓库内部主键，关联 inventory.warehouses.id。
    /// </summary>
    public long WarehouseId { get; set; }

    /// <summary>
    /// 商品变体内部主键，关联 catalog.product_variants.id。
    /// </summary>
    public long VariantId { get; set; }

    /// <summary>
    /// 仓库实际持有数量，包含已预占部分，必须非负。
    /// </summary>
    public int QuantityOnHand { get; set; }

    /// <summary>
    /// 已预占但尚未出库的数量，介于零和实物库存之间。
    /// </summary>
    public int QuantityReserved { get; set; }

    /// <summary>
    /// 补货预警阈值，单位为件，必须非负。
    /// </summary>
    public int ReorderLevel { get; set; }

    /// <summary>
    /// 记录最后更新时间，由写入方显式维护。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<StockMovement> StockMovements { get; set; } =
        new List<StockMovement>();

    public virtual ProductVariant Variant { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
