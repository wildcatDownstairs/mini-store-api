# Marketing：优惠券与核销

[返回模块学习导航](../README.md)

后台维护优惠券规则与启停。用户使用优惠券发生在 Checkout：报价验证优惠资格，下单成功时写核销并增加使用次数。

## 文件怎么读

先看 `MarketingEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Coupon.cs](Coupon.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [CouponRedemption.cs](CouponRedemption.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [MarketingDtos.cs](MarketingDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [MarketingEndpoints.cs](MarketingEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [MarketingService.cs](MarketingService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

维护：`MarketingEndpoints → MarketingService.CreateAsync / UpdateAsync → Fill → SaveChangesAsync`。使用：`CheckoutService.ReadAsync → Pricing.Calculate → PlaceAsync → coupon_redemptions + used_count`。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/admin/coupons` | 后台只读或运营 |
| POST | `/api/admin/coupons` | 后台运营 |
| PUT | `/api/admin/coupons/{id}` | 后台运营 |
| PATCH | `/api/admin/coupons/{id}/status` | 后台运营 |

## 数据库关系与业务规则

`marketing.coupons` 保存规则；`marketing.coupon_redemptions` 关联优惠券、客户和订单并保存实际优惠快照，不依赖以后规则的重新计算。

- fixed 的 DiscountValue 是减去的日元金额；percentage 的 10 表示减 10%，不是打 1 折。
- 结算同时检查启用、起止时间、最低消费、使用上限与最高优惠额；折扣不会超过商品小计。
- 列表 status=active 仅筛选 IsActive，不保证优惠券当前未过期或尚有额度。
- 已核销券不可改优惠码、计价规则或有效期；仍可改名称、启停与不低于已用次数的上限。
- 下单仅在实际折扣大于零时核销。取消订单保留已用次数和核销记录，当前不会自动返还额度。

## 这里可以学到什么

时间请求使用 DateTimeOffset 保留输入时区，写入 PostgreSQL 前转换 UTC。优惠券是一行共享资源，下单和编辑锁定同一行，避免同时突破次数上限。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT c.code, c.discount_type, c.discount_value, c.used_count,
       count(r.id) AS redemption_count, coalesce(sum(r.discount_amount), 0) AS discount_total
FROM marketing.coupons c
LEFT JOIN marketing.coupon_redemptions r ON r.coupon_id = c.id
GROUP BY c.id
ORDER BY c.created_at DESC, c.id DESC
LIMIT 10;
```

规则金额和实际优惠总额不是同一概念；实际发生的优惠要汇总核销表，而不是简单用优惠值乘次数。

## 建议动手顺序

选一个 percentage 优惠券，手工计算达到门槛、最高优惠限制后的结果，再对照 `CheckoutService.ReadAsync` 与报价响应，不直接修改已核销记录。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Checkout](../Checkout/README.md)、[Orders](../Orders/README.md)。
