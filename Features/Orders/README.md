# Orders：订单与成交快照

[返回模块学习导航](../README.md)

查询订单历史和详情，处理未付款取消、确认与配货。创建订单属于 Checkout，付款属于 Payments，发货和签收属于 Shipping，避免把所有动作堆在订单端点。

## 文件怎么读

先看 `OrderEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Order.cs](Order.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [OrderAddress.cs](OrderAddress.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [OrderDtos.cs](OrderDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [OrderEndpoints.cs](OrderEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [OrderItem.cs](OrderItem.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [OrderService.cs](OrderService.cs) | 本模块查询、校验、业务规则和事务。 |
| [OrderStatusHistory.cs](OrderStatusHistory.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [OrderSummary.cs](OrderSummary.cs) | 数据库只读视图结果映射。 |

## 一次请求如何经过本模块

`OrderEndpoints → OrderService.DetailAsync → 显式加载快照/支付/物流 → OrderDetailDto`。客户入口传入内部客户 ID 限制归属，后台入口传 null 表示跨客户查询，必须先通过后台权限。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/me/orders` | 客户本人 |
| GET | `/api/me/orders/{id}` | 客户本人 |
| POST | `/api/me/orders/{id}/cancel` | 客户本人 |
| GET | `/api/admin/orders` | 后台只读或运营 |
| GET | `/api/admin/orders/{id}` | 后台只读或运营 |
| POST | `/api/admin/orders/{id}/cancel` | 后台运营 |
| POST | `/api/admin/orders/{id}/confirm` | 后台运营 |
| POST | `/api/admin/orders/{id}/process` | 后台运营 |

## 数据库关系与业务规则

`sales.orders` → `sales.order_items`、`sales.order_addresses`、`sales.order_status_history`；关联客户、支付、物流和库存流水。`OrderSummary` 映射只读视图 `sales.order_summary`，当前服务直接从基础表生成 DTO。

- 历史商品名称、SKU、价格来自 order_items，历史地址来自 order_addresses；客户当前姓名来自 customers，不是姓名快照。
- Checkout 新订单先记录 pending → confirmed 历史，订单当前状态直接为 confirmed；之后支付 → paid，配货 → processing，发货 → shipped，签收 → delivered。
- 只有 pending / confirmed 且未付款订单可取消。取消释放本人订单的预占，不扣实物库存；优惠核销与使用次数保留。
- Transition 只同步状态、时间和历史对象，不验证状态转换，也不自动保存；调用业务方法必须先验证并提交。
- ReleaseAsync 必须在调用方事务内执行。发货时同时释放预占、扣实物、追加流水；它不会单独 Commit。

## 这里可以学到什么

`Include/ThenInclude` 指定关系加载，`AsSplitQuery` 避免多个集合产生乘积式重复数据。数据库外键保护历史关联，软删除或停用商品不应该删除历史订单项。订单总额是快照，退款也不会直接减写原 GrandTotal。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT o.order_number, o.status, o.grand_total,
       i.sku, i.product_name, i.quantity, i.unit_price, i.line_total
FROM sales.orders o
JOIN sales.order_items i ON i.order_id = o.id
ORDER BY o.created_at DESC, o.id DESC, i.id
LIMIT 10;
```

一张订单有多个订单项，所以 JOIN 后订单号和 GrandTotal 会重复；不要对这些重复行直接 SUM(grand_total)。

## 建议动手顺序

读取自己的订单详情，比较商品当前名称与订单项 ProductName。再沿 `CancelAsync → ReleaseAsync → Transition` 看取消为何需要一整个事务。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Checkout](../Checkout/README.md)、[Payments](../Payments/README.md)、[Shipping](../Shipping/README.md)、[Inventory](../Inventory/README.md)。
