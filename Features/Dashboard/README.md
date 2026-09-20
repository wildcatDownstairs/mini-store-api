# Dashboard：运营统计

[返回模块学习导航](../README.md)

只读聚合订单、退款、商品、客户和待办信息。它没有独立业务表或实体，只需要 Endpoints、Service 和响应 DTO。

## 文件怎么读

先看 `DashboardEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [DashboardDtos.cs](DashboardDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [DashboardEndpoints.cs](DashboardEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [DashboardService.cs](DashboardService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

`DashboardEndpoints → DashboardService.GetAsync → 多个 SQL 聚合查询 → DashboardDto`。每个查询顺序 await；同一个 DbContext 不能并行执行多个数据库操作。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/admin/dashboard` | 后台只读或运营 |

## 数据库关系与业务规则

读取 `sales.orders`、`sales.order_items`、`payment.refunds`、`catalog.products`、`account.customers`、`inventory.stocks`、`review.product_reviews`。这些表仍属于各自业务功能，Dashboard 不拥有它们。

- GMV 与已付款订单数按 PaidAt 最近 30 天、JPY 币种筛选；GMV 含税和运费，不扣退款。
- AOV = GMV / 已付款订单数，零订单时为零；退款按退款完成时间在最近 30 天独立统计，未必对应同一批成交订单。
- 销量 TOP 5 按已付款订单商品件数汇总，显示当前商品名称和品牌，不扣退款或退货数量。
- 状态分布统计全部订单；商品、客户、低库存、待审退款/评价是当前汇总，不受 30 天窗口限制。
- 每日趋势只返回存在成交的日期，不补零；多条查询没有统一快照事务，实时写入时各指标可能存在短暂差异。

## 这里可以学到什么

`GroupBy` 表示分组，`Sum/Count` 表示聚合，`OrderBy` 决定排序，`Take(5)` 限制结果。不要为总额加载十万条订单到 C# 再逐行累加，交给 PostgreSQL 完成聚合。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT (paid_at AT TIME ZONE 'UTC')::date AS day,
       count(*) AS paid_orders, sum(grand_total) AS gmv
FROM sales.orders
WHERE paid_at >= now() - interval '30 days' AND currency = 'JPY'
GROUP BY (paid_at AT TIME ZONE 'UTC')::date
ORDER BY day;
```

显式选择 UTC 日界线，避免会话时区影响日期分组；可把 UTC 换成 Asia/Tokyo 比较日本营业日统计差异。

## 建议动手顺序

对比 GMV 与退款金额的时间范围，解释为什么它们相减不一定代表这批订单的净收入。再在只读查询前加 EXPLAIN，观察时间筛选和聚合计划。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Orders](../Orders/README.md)、[Payments](../Payments/README.md)、[Inventory](../Inventory/README.md)。
