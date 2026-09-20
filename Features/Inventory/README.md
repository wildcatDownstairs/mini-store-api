# Inventory：库存与流水

[返回模块学习导航](../README.md)

后台查询仓库、当前库存及流水，并登记人工调整。下单预占由 Checkout 调数据库函数，取消/发货的释放逻辑由 OrderService 配合完成。

## 文件怎么读

先看 `InventoryEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [InventoryDtos.cs](InventoryDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [InventoryEndpoints.cs](InventoryEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [InventoryService.cs](InventoryService.cs) | 本模块查询、校验、业务规则和事务。 |
| [Stock.cs](Stock.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [StockMovement.cs](StockMovement.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [Warehouse.cs](Warehouse.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |

## 一次请求如何经过本模块

`InventoryEndpoints → InventoryService.AdjustAsync → 查找仓库/规格内部键 → 条件 UPDATE → 追加 adjustment 流水 → CommitAsync`。传入 Quantity 是增减量，而不是调整后的库存总数。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/admin/inventory/warehouses` | 后台只读或运营 |
| GET | `/api/admin/inventory/stocks` | 后台只读或运营 |
| GET | `/api/admin/inventory/stock-movements` | 后台只读或运营 |
| POST | `/api/admin/inventory/stocks/{warehouse}/{variant}/adjust` | 后台运营 |

## 数据库关系与业务规则

`inventory.warehouses` → `inventory.stocks` → `inventory.stock_movements`。Stock 还关联 `catalog.product_variants`，订单流水关联 `sales.orders`。预占函数与约束共同防止超卖。

- 库存以 (warehouse_id, variant_id) 为联合主键；同一规格在不同仓库有不同的库存记录。
- 可售量 = 实物 quantity_on_hand − 预占 quantity_reserved。CHECK 保证实物非负、预占非负且不超过实物。
- 预占只增加 reserved，不扣 on_hand；发货同时减少两者，取消只释放 reserved。
- reservation/release 描述预占变化，purchase/sale/return/adjustment 描述实物变化；不能把所有流水 quantity 无差别相加算实物库存。
- 人工调整把校验写进 UPDATE 条件，并检查影响行数；调整后实物不能少于已预占数量。更新与流水同事务提交。

## 这里可以学到什么

“先读余额，再写新余额”可能丢失并发修改。这里使用条件 UPDATE 原子校验，再配合事务保存审计记录。`FromSql`/`ExecuteSqlAsync` 的插值参数交给驱动绑定；不要拼接用户提供的 SQL。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT w.code, v.sku, s.quantity_on_hand, s.quantity_reserved,
       s.quantity_on_hand - s.quantity_reserved AS available, s.reorder_level
FROM inventory.stocks s
JOIN inventory.warehouses w ON w.id = s.warehouse_id
JOIN catalog.product_variants v ON v.id = s.variant_id
WHERE s.quantity_on_hand - s.quantity_reserved < s.reorder_level
ORDER BY w.id, v.id
LIMIT 10;
```

WHERE 在数据库筛选低于补货线的记录；同一个 SKU 可以出现多次，因为每行对应不同仓库。

## 建议动手顺序

先只读比较一个 SKU 在各仓库的实物和预占量，再在隔离测试里观察下单、取消、发货分别生成哪些流水，不直接改历史流水凑余额。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Products](../Products/README.md)、[Checkout](../Checkout/README.md)、[Orders](../Orders/README.md)、[Shipping](../Shipping/README.md)。
