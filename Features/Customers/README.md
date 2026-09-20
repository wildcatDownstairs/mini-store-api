# Customers：客户与地址

[返回模块学习导航](../README.md)

商城维护本人资料与日本地址，后台查询客户、地址和近期订单，并控制账号启停。当前只支持 JP 地址；姓名和地址字段保留原样作为业务数据。

## 文件怎么读

先看 `CustomerEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Customer.cs](Customer.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [CustomerAddress.cs](CustomerAddress.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [CustomerDtos.cs](CustomerDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [CustomerEndpoints.cs](CustomerEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [CustomerService.cs](CustomerService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

`CustomerEndpoints → user.ActorId() → CustomerService.SaveAddress → account.customer_addresses`。地址 UUID 来自路径，内部客户 ID 来自认证，两者同时出现在查询条件中，防止修改他人的地址。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/me` | 客户本人 |
| PUT | `/api/me` | 客户本人 |
| GET | `/api/me/addresses` | 客户本人 |
| POST | `/api/me/addresses` | 客户本人 |
| PUT | `/api/me/addresses/{id}` | 客户本人 |
| DELETE | `/api/me/addresses/{id}` | 客户本人 |
| GET | `/api/admin/customers` | 后台只读或运营 |
| GET | `/api/admin/customers/{id}` | 后台只读或运营 |
| PATCH | `/api/admin/customers/{id}/status` | 后台运营 |

## 数据库关系与业务规则

`account.customers` 一对多关联 `account.customer_addresses` 与 `sales.orders`。地址默认规则通过带 WHERE 条件的唯一索引实现，仅限制标为默认的记录。

- 每个客户最多 20 个地址。每种 address_type（shipping/billing）最多一个默认地址；新增该类型首个地址自动设为默认。
- 设置默认地址时先锁定客户行，在同一事务里取消旧默认并设置新默认，数据库部分唯一索引进一步兜底。
- 已有地址类型不能直接修改。删除默认地址时会为同类型剩余地址补选默认项。
- 订单地址是独立快照，修改或删除地址簿条目不会改写已下单的地址。
- 客户列表 TotalSpent 统计已付款 JPY 订单的原成交金额，未扣除退款，不应直接称为净收入。

## 这里可以学到什么

`Guid` 地址标识用于 API，`long CustomerId` 用于数据库关联。`DateOnly?` 表示可选的生日日期；它不同于保存业务时间点的 `DateTime`。`ExecuteUpdateAsync` 直接执行 SQL，不等待 SaveChanges，不能误认为已加载实体会自动同步。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT c.public_id, c.last_name, c.first_name,
       count(a.id) AS address_count,
       count(a.id) FILTER (WHERE a.is_default AND a.address_type = 'shipping') AS shipping_defaults
FROM account.customers c
LEFT JOIN account.customer_addresses a ON a.customer_id = c.id
GROUP BY c.id
ORDER BY c.created_at DESC, c.id DESC
LIMIT 5;
```

FILTER 只在聚合计数时应用条件；没有地址的客户仍由 LEFT JOIN 保留，COUNT(a.id) 返回零。

## 建议动手顺序

登录后读取 `/api/me/addresses`，对照 `AddressDto.Id` 和数据库 `public_id`。在测试账号中添加两条收货地址，观察切换默认项时始终只有一个默认地址。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Auth](../Auth/README.md)、[Checkout](../Checkout/README.md)、[Orders](../Orders/README.md)。
