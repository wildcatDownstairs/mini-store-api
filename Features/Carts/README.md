# Carts：购物车

[返回模块学习导航](../README.md)

维护当前客户的一辆活动购物车。加购只记录购买意向，不创建订单、不锁住库存。历史 converted / abandoned 购物车保留在数据库中。

## 文件怎么读

先看 `CartEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Cart.cs](Cart.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [CartDtos.cs](CartDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [CartEndpoints.cs](CartEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [CartItem.cs](CartItem.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [CartService.cs](CartService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

`CartEndpoints → CartService.SetItemAsync → 锁定客户行 → 查找或创建 active cart → 设置 cart_items.quantity → 提交`。路径 `{id}` 是规格 UUID，不是购物车条目 ID。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/me/cart` | 客户本人 |
| PUT | `/api/me/cart/items/{id}` | 客户本人 |
| DELETE | `/api/me/cart/items/{id}` | 客户本人 |

## 数据库关系与业务规则

`sales.carts` → `sales.cart_items` → `catalog.product_variants`；展示同时查询商品、分类与 `inventory.stocks`。购物车条目是可级联删除的子资源，订单项则是需要保留的历史快照。

- 同一客户只能有一个 active cart，由部分唯一索引保障；同一购物车内同一规格只能有一条记录。
- PUT 设置目标数量，不是加法。每种规格允许 1～99 件，购物车最多 50 种商品。
- 加购读取跨仓可售量，但不预占；下单时还会检查是否有单个仓库能完整履约。
- UnitPrice 为当前规格税前价，PreviousUnitPrice 为加购或最后设置数量时保存的价，帮助客户端提示价格变化。
- 空车 Totals 为 null；非空预估金额按无券、标准配送计算，最终金额以 Checkout 报价为准。

## 这里可以学到什么

先锁定客户行能串行化同一客户同时开两个窗口修改购物车的操作；不同客户仍可并发。`ExecuteDeleteAsync` 直接发送 DELETE，删除不存在条目也返回 200，data 为 null，便于重复操作。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT c.public_id, c.customer_id, c.status,
       count(i.id) AS sku_count, coalesce(sum(i.quantity), 0) AS item_count
FROM sales.carts c
LEFT JOIN sales.cart_items i ON i.cart_id = c.id
WHERE c.status = 'active'
GROUP BY c.id
ORDER BY c.created_at DESC, c.id DESC
LIMIT 5;
```

COUNT 统计规格种类，SUM(quantity) 统计商品件数；COALESCE 把空车的 SUM 空值转换成零。

## 建议动手顺序

在测试账号中把同一规格数量从 1 设为 2，再读取 `/api/me/cart`；应看到一条商品、两件数量，库存预占量不会因为加购而增加。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Products](../Products/README.md)、[Inventory](../Inventory/README.md)、[Checkout](../Checkout/README.md)。
