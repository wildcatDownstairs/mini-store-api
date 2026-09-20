# 页面与接口契约

前端只消费 DTO；内部 bigint 关联键不作为 API 资源标识。接口路径和请求类型见 [OpenAPI 快照](openapi.json)，运行时 `/openapi/v1.json` 为最新契约。响应 JSON 使用 camelCase。

分页返回 `items / page / pageSize / total`；失败返回 ProblemDetails，业务描述在 `detail`。401 需要重新登录，403 无权限，409 表示状态、报价、库存或版本已变化，不能盲目重试。

公开查询无需 Token；`/api/me/*` 必须客户登录；`/api/admin/*` 除登录外需要后台身份。运营角色可以写，只读角色只能查询。请求体与 Idempotency-Key 的使用可参考商城 `Checkout.vue` 和可运行的 `scripts/test_api.py`。

| 方法 | 路径 |
|---|---|
| GET | `/health` |
| GET | `/api/config` |
| POST | `/api/admin/auth/login` |
| POST | `/api/me/checkout/quote` |
| POST | `/api/me/orders` |
| GET | `/api/me/orders` |
| POST | `/api/me/orders/{id}/simulate-payment` |
| POST | `/api/admin/orders/{id}/simulate-payment` |
| GET | `/api/store/products/{id}/reviews` |
| POST | `/api/me/orders/{id}/items/{itemId}/review` |
| GET | `/api/admin/dashboard` |
| POST | `/api/auth/register` |
| POST | `/api/auth/login` |
| GET | `/api/store/products` |
| GET | `/api/store/products/{slug}` |
| GET | `/api/store/products/by-id/{id}` |
| GET | `/api/store/brands` |
| GET | `/api/store/categories` |
| GET | `/api/admin/products` |
| POST | `/api/admin/products` |
| GET | `/api/admin/products/{id}` |
| PUT | `/api/admin/products/{id}` |
| GET | `/api/admin/brands` |
| GET | `/api/admin/categories` |
| PATCH | `/api/admin/products/{id}/status` |
| GET | `/api/me` |
| PUT | `/api/me` |
| GET | `/api/me/addresses` |
| POST | `/api/me/addresses` |
| PUT | `/api/me/addresses/{id}` |
| DELETE | `/api/me/addresses/{id}` |
| GET | `/api/admin/customers` |
| GET | `/api/admin/customers/{id}` |
| PATCH | `/api/admin/customers/{id}/status` |
| GET | `/api/me/cart` |
| PUT | `/api/me/cart/items/{id}` |
| DELETE | `/api/me/cart/items/{id}` |
| GET | `/api/me/orders/{id}` |
| POST | `/api/me/orders/{id}/cancel` |
| GET | `/api/admin/orders` |
| GET | `/api/admin/orders/{id}` |
| POST | `/api/admin/orders/{id}/cancel` |
| POST | `/api/admin/orders/{id}/confirm` |
| POST | `/api/admin/orders/{id}/process` |
| GET | `/api/admin/inventory/warehouses` |
| GET | `/api/admin/inventory/stocks` |
| GET | `/api/admin/inventory/stock-movements` |
| POST | `/api/admin/inventory/stocks/{warehouse}/{variant}/adjust` |
| GET | `/api/admin/payments` |
| GET | `/api/admin/refunds` |
| POST | `/api/admin/refunds` |
| POST | `/api/admin/refunds/{id}/review` |
| GET | `/api/admin/shipments` |
| POST | `/api/admin/orders/{id}/ship` |
| POST | `/api/admin/orders/{id}/deliver` |
| GET | `/api/admin/coupons` |
| POST | `/api/admin/coupons` |
| PUT | `/api/admin/coupons/{id}` |
| PATCH | `/api/admin/coupons/{id}/status` |
| GET | `/api/admin/reviews` |
| POST | `/api/admin/reviews/{id}/review` |

## 关键约定

- 产品目录价格为含税展示价；商品维护价格为税前 JPY 整数。订单详情以 totals 和 items 的成交快照为准。
- 先报价，再携带 quoteToken 与原地址/配送/优惠提交；每次独立下单生成新的 Idempotency-Key，网络重试使用原键。
- 库存列表返回仓库与规格 UUID；调整必须提供数量和原因。不要由浏览器直接更新库存总量。
- 后台商品编辑提交 version；订单不提供任意 PATCH status，而提供具体业务动作。
- 评价创建使用已签收订单的 orderItemId；管理员审核后才公开。
- 商品分类/品牌/仓库目前是读取现有数据库元数据，未新增管理页面所没有的维护功能。
- 退款审核只在启用模拟支付的开发环境完成结算；生产不伪造外部网关成功。

## 响应 DTO 阅读说明

命名 record 会生成明确的 OpenAPI schema；部分查询使用匿名 LINQ 投影，其 OpenAPI 响应只表现为通用 object。学习自动生成客户端时，应先把这些响应提取为命名 record 并声明 Produces 类型。目前两套前端按实际响应字段对接，可同时参考 CatalogEndpoints、OrderService 的投影以及前端 domain 映射；不要把通用 object 当成空响应。
