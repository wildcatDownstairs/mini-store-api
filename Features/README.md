# 从这里开始读后端

如果第一次接触 ASP.NET Core，先看根目录的 [手摸手从零搭建教程](../手摸手从零搭建ASP.NETCore电商项目.md)。这份导航用于理解现有代码，模块 README 用于沿具体业务深入。

## 建议阅读顺序

| 顺序 | 模块 | 先学什么 |
|---|---|---|
| 1 | [Products 商品](Products/README.md) | 路由、依赖注入、DTO、LINQ、分页、乐观并发 |
| 2 | [Auth 认证](Auth/README.md) | 密码哈希、JWT、认证与授权 |
| 3 | [Customers 客户](Customers/README.md) | 登录人归属、地址、一对多、默认项 |
| 4 | [Carts 购物车](Carts/README.md) | PUT 语义、部分唯一索引、现价与历史记录 |
| 5 | [Inventory 库存](Inventory/README.md) | 实物与预占、条件 UPDATE、流水 |
| 6 | [Checkout 结算](Checkout/README.md) | 金额计算、事务、行锁、幂等 |
| 7 | [Orders 订单](Orders/README.md) | 快照、状态历史、取消与释放库存 |
| 8 | [Payments 支付](Payments/README.md) | 多次支付尝试、退款额度与并发保护 |
| 9 | [Shipping 物流](Shipping/README.md) | 跨功能事务、出库、签收 |
| 10 | [Marketing 优惠](Marketing/README.md) | 计价规则、有效期与核销记录 |
| 11 | [Reviews 评价](Reviews/README.md) | 真实购买关系、审核、条件更新 |
| 12 | [Dashboard 统计](Dashboard/README.md) | SQL 聚合、指标定义、时间范围 |

## 一次请求的共同路线

```text
浏览器的 HTTP 请求
  → Program 注册的中间件：异常处理、认证、授权等
  → XxxEndpoints：从路径、查询参数、JSON 或认证身份取参数
  → XxxService：验证业务条件，组合查询或协调事务
  → StoreDbContext / Npgsql → PostgreSQL
  → XxxDto → ASP.NET Core 序列化为 camelCase JSON
```

后台授权通过不等于可以忽略客户归属；`/api/me/*` 的内部客户 ID 从认证结果读取，不接受请求体自行指定。数据库还有 CHECK、外键、唯一索引与触发器，防止绕过 API 后破坏核心关系。

两个应用级入口直接放在 `Program.cs`，不属于单个业务模块：

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/health` | 无需登录，检查数据库连接是否可用 |
| GET | `/api/config` | 无需登录，返回是否启用模拟支付等公开配置，不返回密钥 |

## 四类文件如何分工

- **实体**如 `Product.cs`：C# 对象如何表示数据库记录及关系；实体中的 `Id` 通常为内部 bigint。
- **DTO**如 `ProductDtos.cs`：本次请求或响应需要哪些数据；对外 `Id` 通常映射实体的 `PublicId`，不要根据名字混淆。
- **Service**：业务校验、查询投影、写入及事务。类名后的构造参数由依赖注入提供，一次请求共用 Scoped DbContext。
- **Endpoints**：路由、访问策略、参数绑定、调用服务和 HTTP 结果。端点注册本身不会查询数据库。

`Data/StoreDbContext.cs` 统一映射多个 schema，让跨功能业务共用事务。Dashboard 不拥有新表，不需要凑一个实体；Checkout 的 `Pricing` 是独立纯计价函数。

Endpoints 上的 `WithName`、`WithSummary`、`WithDescription` 和 `ProducesProblem` 描述接口文档；请求 record 的 XML `param` 注释说明 JSON 字段。共享 [OpenApiDocumentation](../Common/OpenApiDocumentation.cs) 根据真实授权元数据生成 JWT 声明，补充通用参数与错误说明。开发环境打开 Scalar `/scalar` 可按这些说明调试；文档声明不能替代业务校验。

## 经常遇到的 C# 写法

| 写法 | 在本项目中的含义 |
|---|---|
| `record` | 适合请求和响应数据；ASP.NET Core 可从 JSON 调用构造函数，不必显式 new |
| `class ProductService(StoreDbContext db)` | 主构造函数；容器创建服务时提供 db，不是全局变量 |
| `Task<T>`、`async/await` | 异步等待并最终得到 T；不表示自动并行执行 SQL |
| `CancellationToken ct` | 向数据库传递请求取消；超时或断线不证明事务一定未提交 |
| `Guid` / `long` | 对应 API UUID / 数据库内部 bigint，别把两种 ID 混用 |
| `decimal` | 精确金额；当前业务校验整数 JPY，数据库仍使用 NUMERIC |
| `DateTime?` | 可空的时间点，例如订单还没有 paid_at；应用按 UTC 处理 |
| `null!` | 告诉编译器该属性由 EF 填充，不是运行时非空检查 |
| `partial` | 同一个类可分文件，例如 DbContext 和并发映射 |
| `virtual` 导航属性 | 表示关联对象/集合；本项目没启用懒加载，访问属性不会自动查库 |
| `=>` | 表达式形式的方法体或 lambda；在 IQueryable 中常被翻译为 SQL |

## 常见 EF / SQL 对照

| 代码 | 数据库行为或边界 |
|---|---|
| `Where` | WHERE；查询真正执行前仍只是表达式 |
| `Select` | 投影需要的列；构造 DTO，避免输出整个实体图 |
| `OrderBy/ThenBy` | ORDER BY；分页要有稳定的次级键 |
| `Skip/Take` | OFFSET/LIMIT；跨请求新增数据仍可能使页码移动 |
| `CountAsync/ToListAsync` | 真正执行查询；同一个 DbContext 不应同时跑多个异步操作 |
| `AsNoTracking` | 只读查询不登记实体修改，减少跟踪开销 |
| `Include/ThenInclude` | 显式加载关联；本项目也常直接 Select 导航字段让 EF 生成关联 SQL |
| `SaveChangesAsync` | 将跟踪的新增/修改/删除写到数据库；有外层事务时仍需 Commit |
| `ExecuteUpdateAsync/ExecuteDeleteAsync` | 立即执行 SQL，不经过跟踪器，也不自动刷新已经加载的对象 |
| `BeginTransactionAsync/CommitAsync` | 多条写入共同成功；未提交退出时回滚 |
| `FOR UPDATE` | 在事务中锁定相关行；锁定顺序一致可降低死锁风险 |
| `xmin` | PostgreSQL 行版本；本项目通过影子属性 Version 做商品并发检查 |

源码注释说明职责、前提与关键原因，不给每个大括号或赋值重复加解释。实体标量字段已有数据库中文含义，导航属性、服务方法、DTO 和关键事务步骤也有说明；更完整的操作流程放在各模块 README。

## 怎样验证自己的修改

从项目根目录执行 `dotnet build` 与 `dotnet csharpier check .`。改了数据库查询、状态或金额逻辑后，再运行 `.venv/bin/python scripts/test_api.py` 验证真实 PostgreSQL 上的行为。编译通过不代表 LINQ 一定能翻译成 SQL，也不代表并发操作正确。

完整业务链与既有验证记录分别见 [学习路线](../docs/backend/learning-guide.md) 和 [验证报告](../docs/backend/verification.md)。
