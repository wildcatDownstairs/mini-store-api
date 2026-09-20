# Shipping：发货与签收

[返回模块学习导航](../README.md)

后台查询物流并登记发货、确认送达。这里记录本地履约状态，不会调用真实 Yamato、Sagawa 或 Japan Post 接口。

## 文件怎么读

先看 `ShippingEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Shipment.cs](Shipment.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [ShippingDtos.cs](ShippingDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [ShippingEndpoints.cs](ShippingEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [ShippingService.cs](ShippingService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

`ShippingEndpoints → ShippingService.ShipAsync → 锁订单 → 校验预占仓 → OrderService.ReleaseAsync(sale: true) → 写物流 → Transition(shipped) → CommitAsync`。签收由 `DeliverAsync` 更新物流和订单。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/admin/shipments` | 后台只读或运营 |
| POST | `/api/admin/orders/{id}/ship` | 后台运营 |
| POST | `/api/admin/orders/{id}/deliver` | 后台运营 |

## 数据库关系与业务规则

`shipping.shipments` 关联 `sales.orders` 和 `inventory.warehouses`；发货还会写库存与订单历史，不能只插入物流行就宣称已经出库。

- 必须已付款且为 processing 才能发货；仅支持 Yamato、Sagawa、Japan Post，运单号必须符合校验格式。
- 发货仓必须与订单尚未释放的预占仓一致。当前只支持单仓完整履约，不支持拆包、拆仓或部分发货。
- 已有多条物流或已发运物流时拒绝重复发货；只有 pending/ready 的既有单条物流可被用于本次发货。
- 库存释放、实物扣减、库存流水、物流和订单状态在同一事务内完成。
- 确认送达要求订单为 shipped，所有关联物流为 shipped/in_transit；完成后才能提交购买评价。

## 这里可以学到什么

跨功能服务可以共享同一个 Scoped DbContext，所以 ShippingService 调用 OrderService 时能参加当前事务。业务动作端点比任意修改 status 更能保护完整规则。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT o.order_number, o.status AS order_status, s.carrier,
       s.tracking_number, s.status AS shipment_status, s.shipped_at, s.delivered_at
FROM shipping.shipments s
JOIN sales.orders o ON o.id = s.order_id
ORDER BY s.created_at DESC, s.id DESC
LIMIT 5;
```

订单状态与物流状态来自不同表，但需要遵守业务时间线；尚未送达时 delivered_at 可以为空。

## 建议动手顺序

沿 `ShipAsync` 找到提交事务的位置，逐项列出出库失败时哪些写入必须一起回滚，再阅读集成测试中的发货与签收请求。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Orders](../Orders/README.md)、[Inventory](../Inventory/README.md)、[Reviews](../Reviews/README.md)。
