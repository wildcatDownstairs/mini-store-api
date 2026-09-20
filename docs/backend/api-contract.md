# 页面与接口契约

前端只消费 DTO；内部 bigint 关联键不作为 API 资源标识。接口路径和请求类型见 [OpenAPI 快照](openapi.json)，运行时 `/openapi/v1.json` 为最新契约。响应 JSON 使用 camelCase。开发环境访问 `/scalar` 可按中文分组调试；Authentication 中选择 Bearer 并输入令牌本体。

全部业务与系统接口声明 WithName、WithSummary、WithDescription 和错误响应。请求字段由 XML 注释生成，JWT 权限从真实路由元数据推导。文档说明不替代运行时校验；下单必须带非空 Idempotency-Key。

所有接口统一返回 `success / code / msg / data`，不提供 `message` 字段。分页返回 `data.records / current / size / total / pages` 等公司字段（完整示例见下文）；失败描述在 `msg`。401 需要重新登录，403 无权限，409 表示状态、报价、库存或版本已变化，不能盲目重试。

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

接口响应使用各功能 `*Dtos.cs` 中的具名 record，Service 返回明确类型，OpenAPI 可展示商品、订单、购物车、库存等结构。商品创建使用 `TypedResults.Created`，下单端点明确声明首次创建 201 与幂等重放 200；无业务数据的修改操作使用 200，data 为 null。可以从 `ProductService`、`OrderService` 的查询投影追踪字段来源。匿名类型仅用于内部查询中间结果和报价摘要，不作为业务 API 的响应契约。


## 公司统一响应结构

成功示例（商品详情）：

```json
{"success": true, "code": 200, "msg": "Success", "data": {"id": "商品公开 UUID", "name": "日常のカップ"}}
```

失败示例（HTTP 409）：

```json
{"success": false, "code": 409, "msg": "库存、退款额度或记录状态已变化，请刷新后重试。", "data": null}
```

分页示例（`GET /api/store/products?page=1&pageSize=20`，无匹配商品）：

```json
{
  "success": true,
  "code": 200,
  "msg": "Success",
  "data": {
    "countId": "",
    "current": 1,
    "maxLimit": 100,
    "optimizeCountSql": true,
    "orders": [],
    "pages": 0,
    "records": [],
    "searchCount": true,
    "size": 20,
    "total": 0
  }
}
```

字段结构对齐公司 TableModel；`maxLimit` 如实使用本服务已有的 100 条上限（参考项目为 500），请求仍使用 `page/pageSize`。`pages` 为总数除以每页条数后向上取整；越界页的 `records` 为空，但保留实际 `total/pages`。`countId/optimizeCountSql/orders` 为契约兼容字段，本服务不使用 MyBatis：排序实际由 Service 决定，计数由 EF Core 执行。

`code` 与 HTTP 状态一致，创建商品及首次下单均为 201。原 204 操作统一返回 HTTP 200 和 `{"success":true,"code":200,"msg":"Success","data":null}`，因为 HTTP 204 不允许响应体。品牌、地址、分类等非分页列表仍是 `data` 中的数组；订单详情和购物车里的 `items` 是业务字段，保留原名。

`Common/ApiResponse.cs` 定义外壳和分页类型；Endpoints 包装成功 DTO，`ApiExceptionHandler` 及状态码中间件包装错误。`AddProblemDetails` 只满足框架异常中间件依赖，不改变实际返回结构。OpenAPI JSON、Scalar 页面与静态资源不包裹。商城和管理后台只在统一 HTTP 入口解包 `data`，错误只读取 `msg`。
