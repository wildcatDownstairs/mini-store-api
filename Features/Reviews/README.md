# Reviews：购买评价与审核

[返回模块学习导航](../README.md)

客户对已签收订单中的具体订单项评价，后台发布或拒绝。商城只展示上架且未软删除商品的 published 评价。

## 文件怎么读

先看 `ReviewEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [ProductReview.cs](ProductReview.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [ReviewDtos.cs](ReviewDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [ReviewEndpoints.cs](ReviewEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [ReviewService.cs](ReviewService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

`ReviewEndpoints → ReviewService.CreateAsync → 锁定本人订单 → 验证 delivered 与订单项归属 → 创建 pending 评价`；后台 `ModerateAsync` 把 pending 变为 published 或 rejected。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/store/products/{id}/reviews` | 无需登录 |
| POST | `/api/me/orders/{id}/items/{itemId}/review` | 客户本人 |
| GET | `/api/admin/reviews` | 后台只读或运营 |
| POST | `/api/admin/reviews/{id}/review` | 后台运营 |

## 数据库关系与业务规则

`review.product_reviews` 关联客户、商品及可选的订单项。数据库可容纳历史无订单项评价，但当前创建 API 只允许有已签收订单的评价。

- 评分为 1～5，标题最多 160 字，正文最多 1000 字；可以只有评分。
- 路径中的 id 是订单 UUID，itemId 是订单项 UUID，不是商品 UUID。必须同时核对订单归属与订单项关系。
- IsVerifiedPurchase 由服务器根据真实关系设置，不能相信客户端自己声明已购买。
- 数据库触发器检查购买、商品、客户和送达时间关系，订单项唯一约束阻止重复购买评价。
- 审核使用带 status=pending 条件的 UPDATE 并检查影响行数，避免两个运营人员互相覆盖结果。公开作者仅显示姓氏加称呼。

## 这里可以学到什么

`short` 足以表示 1～5 星，但类型范围并不等于业务范围，所以仍需校验。`ExecuteUpdateAsync` 直接执行 SQL，无需再 SaveChanges。状态合法性与记录归属都必须在服务端判断。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT p.public_id, p.name, count(r.id) AS review_count,
       round(avg(r.rating), 2) AS average_rating
FROM catalog.products p
JOIN review.product_reviews r ON r.product_id = p.id
WHERE r.status = 'published'
GROUP BY p.id
ORDER BY review_count DESC, p.id
LIMIT 5;
```

先过滤 published 再聚合；HAVING 可在 GROUP BY 之后筛选“评价数至少为某值”的商品。

## 建议动手顺序

在测试流程中对比审核前后 `/api/store/products/{id}/reviews` 的结果，确认后台可见不代表商城已发布。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Orders](../Orders/README.md)、[Shipping](../Shipping/README.md)、[Products](../Products/README.md)。
