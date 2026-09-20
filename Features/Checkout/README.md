# Checkout：报价与下单

[返回模块学习导航](../README.md)

把购物车转换成订单，是跨商品、地址、优惠券、库存和订单的事务协调入口。客户端提交地址、配送方式与优惠码，不提交可信的最终金额。

## 文件怎么读

先看 `CheckoutEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [CheckoutDtos.cs](CheckoutDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [CheckoutEndpoints.cs](CheckoutEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [CheckoutRequest.cs](CheckoutRequest.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [CheckoutService.cs](CheckoutService.cs) | 本模块查询、校验、业务规则和事务。 |
| [Pricing.cs](Pricing.cs) | 不访问数据库的计价纯函数。 |

## 一次请求如何经过本模块

`QuoteAsync → ReadAsync → Pricing.Calculate → 受保护的 QuoteToken`；随后 `PlaceAsync → 幂等检查 → 校验报价与当前数据 → 选单仓 → 保存订单快照 → reserve_stock → 核销优惠 → 转换购物车 → 保存幂等结果 → CommitAsync`。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| POST | `/api/me/checkout/quote` | 客户本人 |
| POST | `/api/me/orders` | 客户本人 |

## 数据库关系与业务规则

本模块实体 `CheckoutRequest` 映射 `sales.checkout_requests`，唯一约束为客户加请求键。下单还写入 Orders、Inventory、Marketing、Carts 的表；调用数据库函数见 [07_functions.sql](../../db/07_functions.sql)。

- 报价有效 10 分钟，不预占库存。下单重新读取现价、地址、优惠及购物车，变化或失效返回冲突。
- 每次独立下单生成新的 Idempotency-Key；网络重试必须复用同一键和完全相同请求。成功重放返回原订单及 200，首次创建返回 201。
- 先检查已完成幂等记录，所以购物车已转换或报价后来过期时，合法重试仍能找回原订单；同键不同内容返回 409。
- 先锁客户、商品/规格、优惠券，再按固定顺序锁库存；当前只允许一个仓库满足全部规格，不能把跨仓总量当作单仓能力。
- 商品名称和成交价格复制到订单项；地址复制到 shipping 与 billing 快照。当前两种地址取自同一个收货地址。
- 订单、预占流水、优惠核销、购物车转换与幂等记录在同一事务提交。中途 SaveChanges 获取内部订单 ID，并不意味着外层事务已经提交。

## 这里可以学到什么

`await using` 管理事务生命周期，未 Commit 的异常路径会回滚。Data Protection 保护报价令牌；普通 SHA256 摘要仅用于比较内容。`CancellationToken` 传递请求取消，不能把“没收到响应”等同于“没提交成功”，这正是幂等键的用途。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT r.customer_id, r.request_key, o.public_id, o.order_number,
       o.status, o.grand_total, r.created_at
FROM sales.checkout_requests r
JOIN sales.orders o ON o.id = r.order_id
ORDER BY r.created_at DESC, r.customer_id, r.request_key
LIMIT 5;
```

同一客户、同一请求键对应一次成功下单。种子数据可能没有幂等记录，空结果也是正常的。

## 建议动手顺序

先读 `Pricing.Calculate`：税前 1000 × 2，优惠 200，税率 10%，标准运费 500，最终为 2480 JPY。再阅读 `scripts/test_api.py` 中同键并发下单检查。标准配送折后税前满 8000 免运费，快捷配送固定 900。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Carts](../Carts/README.md)、[Orders](../Orders/README.md)、[Inventory](../Inventory/README.md)、[Marketing](../Marketing/README.md)。
