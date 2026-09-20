# Payments：支付与退款

[返回模块学习导航](../README.md)

记录支付尝试、模拟付款、申请退款和审核退款。当前没有真实网关扣款、银行卡信息采集或支付回调；模拟功能必须在开发环境显式开启。

## 文件怎么读

先看 `PaymentEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Payment.cs](Payment.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [PaymentDtos.cs](PaymentDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [PaymentEndpoints.cs](PaymentEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [PaymentService.cs](PaymentService.cs) | 本模块查询、校验、业务规则和事务。 |
| [Refund.cs](Refund.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |

## 一次请求如何经过本模块

付款：`SimulateCustomerAsync / SimulateAdminAsync → RequireSimulation → Simulate → 锁订单 → 写支付 → 更新订单状态`。退款：`RequestRefundAsync → 锁支付 → 检查可退额度 → pending`，再由 `ReviewRefundAsync` 完成或拒绝。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| POST | `/api/me/orders/{id}/simulate-payment` | 客户本人 |
| POST | `/api/admin/orders/{id}/simulate-payment` | 后台运营 |
| GET | `/api/admin/payments` | 后台只读或运营 |
| GET | `/api/admin/refunds` | 后台只读或运营 |
| POST | `/api/admin/refunds` | 后台运营 |
| POST | `/api/admin/refunds/{id}/review` | 后台运营 |

## 数据库关系与业务规则

`payment.payments` 多对一关联 `sales.orders`；`payment.refunds` 多对一关联支付。外部交易号和退款号有唯一性约束，模拟记录用 SIM 前缀区分。

- 支付金额来自订单 GrandTotal，不能由浏览器随意传金额。失败后可以重试，因此一个订单可能有多次支付尝试。
- 已付款订单重复成功模拟只返回 AlreadyPaid，不创建第二笔收款；新尝试响应包含 Success。
- 可申请退款金额 = 已收款金额 − 已完成退款 − 待审核退款；pending 也占额度。
- 申请和审核都会锁定同一支付行；数据库 guard_refund 触发器再次检查，防止并发超额和绕过 API 的不合法写入。
- 拒绝申请标记 failed；批准模拟退款只在允许模拟资金结算时执行。退款不等于退货入库，也不改原订单金额。

## 这里可以学到什么

事务锁让两个同时退款的请求按顺序检查余额。`decimal` 对应精确金额，`DateTime? CapturedAt` 为空表示尚未完成收款。API 的业务校验负责友好错误，PostgreSQL 约束与触发器负责最后的数据完整性防线。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT p.public_id, p.status, p.amount, p.currency,
       coalesce(sum(r.amount) FILTER (WHERE r.status = 'completed'), 0) AS completed_refunds,
       coalesce(sum(r.amount) FILTER (WHERE r.status = 'pending'), 0) AS pending_refunds
FROM payment.payments p
LEFT JOIN payment.refunds r ON r.payment_id = p.id
WHERE p.captured_at IS NOT NULL
GROUP BY p.id
ORDER BY p.created_at DESC, p.id DESC
LIMIT 5;
```

先按支付聚合退款再判断余额，避免一笔支付因多条退款而被重复计算；币种必须保持一致。

## 建议动手顺序

阅读 `scripts/test_api.py` 的并发退款检查：两次申请各自看似合法，合计超额时只能允许其中一次成功。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Orders](../Orders/README.md)、[Shipping](../Shipping/README.md)。
