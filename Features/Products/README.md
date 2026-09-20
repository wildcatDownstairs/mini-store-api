# Products：商品目录

[返回模块学习导航](../README.md)

向商城提供商品列表、搜索、详情和分类，向后台提供商品创建、编辑、上下架。品牌、分类和图片实体归商品功能，当前 API 没有独立的品牌/分类写入和图片上传接口。

## 文件怎么读

先看 `ProductEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [Brand.cs](Brand.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [Category.cs](Category.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [Product.cs](Product.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [ProductDtos.cs](ProductDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [ProductEndpoints.cs](ProductEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [ProductImage.cs](ProductImage.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [ProductInventorySummary.cs](ProductInventorySummary.cs) | 数据库只读视图结果映射。 |
| [ProductSalesSummary.cs](ProductSalesSummary.cs) | 数据库只读视图结果映射。 |
| [ProductService.cs](ProductService.cs) | 本模块查询、校验、业务规则和事务。 |
| [ProductVariant.cs](ProductVariant.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |

## 一次请求如何经过本模块

`ProductEndpoints.MapProducts → ProductService.ListAsync → StoreDbContext.Products → ProductSummaryDto → JSON`。`ListQuery` 从 URL 查询参数绑定；`admin` 是端点固定传入的布尔值，不是用户可提交的权限开关。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| GET | `/api/store/products` | 无需登录 |
| GET | `/api/store/products/{slug}` | 无需登录 |
| GET | `/api/store/products/by-id/{id}` | 无需登录 |
| GET | `/api/store/brands` | 无需登录 |
| GET | `/api/store/categories` | 无需登录 |
| GET | `/api/admin/products` | 后台只读或运营 |
| POST | `/api/admin/products` | 后台运营 |
| GET | `/api/admin/products/{id}` | 后台只读或运营 |
| PUT | `/api/admin/products/{id}` | 后台运营 |
| GET | `/api/admin/brands` | 后台只读或运营 |
| GET | `/api/admin/categories` | 后台只读或运营 |
| PATCH | `/api/admin/products/{id}/status` | 后台运营 |

## 数据库关系与业务规则

`catalog.products` → `catalog.product_variants` / `catalog.product_images`；产品属于 `catalog.brands`，通过 `catalog.product_categories` 关联 `catalog.categories`。`ProductInventorySummary`、`ProductSalesSummary` 是数据库视图映射，不是额外业务表；当前列表主要查询基础表。

- 公开入口只展示 active 且未软删除商品；后台可读取未软删除的草稿及下架商品。
- Product 是展示主体，ProductVariant 才是实际可购买的 SKU。商品和分类通过 catalog.product_categories 多对多关联。
- 列表与详情展示含税价，后台维护规格 Price 为税前整数日元。食品 8%，其他 10%；分类税率只是本学习库规则。
- 商品编辑必须提交 version。它来自 PostgreSQL xmin，EF 将旧版本放进 UPDATE 条件；过期返回 409，防止并发覆盖。
- 已有规格不能直接删掉，可以停用。新规格在各仓库创建零库存；补货由 Inventory 完成。

## 这里可以学到什么

`IQueryable` 是查询描述，`Where/OrderBy/Select` 组合 SQL；`PageAsync` 才执行计数和分页。`AsNoTracking` 表示只读，不维护修改跟踪。先排序再构造 record，避免 EF 无法翻译 DTO 属性排序。详情用 `Include` 显式加载关系，`AsSplitQuery` 将多个集合拆成查询，减少 JOIN 重复行。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT p.public_id, p.name, b.name AS brand, p.status,
       p.base_price, p.currency, count(v.id) AS variant_count
FROM catalog.products p
JOIN catalog.brands b ON b.id = p.brand_id
LEFT JOIN catalog.product_variants v ON v.product_id = p.id
GROUP BY p.id, b.name
ORDER BY p.created_at DESC, p.id DESC
LIMIT 5;
```

JOIN 找到品牌，LEFT JOIN 保留暂时没有规格的商品；GROUP BY 按商品汇总规格数，ORDER BY 决定先看哪些行。

## 建议动手顺序

请求 `/api/store/products?page=1&pageSize=5&sort=price_asc`，在 `ListAsync` 打断点，把返回的 `id` 与 SQL 的 `public_id` 对照，再比较税前价格和含税显示价。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Inventory](../Inventory/README.md)、[Carts](../Carts/README.md)、[Checkout](../Checkout/README.md)。
