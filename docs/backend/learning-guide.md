# 从 C# 到一次下单

## 1. 从一条 GET 请求认识 ASP.NET Core

运行 API，访问 `/api/store/products?page=1&pageSize=8`。从 `Program.cs` 的 `app.MapProducts()` 找到 `Features/Products/ProductEndpoints.cs`。`MapGet` 把 URL 连接到处理函数，调用 `ProductService.ListAsync`；参数中的 `ProductService service` 由依赖注入提供，不需要手动 new。服务构造参数 `StoreDbContext db` 同样由容器提供，且注册为 Scoped，同一次 HTTP 请求共用一个实例。

按这条链阅读：`ProductEndpoints.cs → ProductService.cs → Product.cs / ProductDtos.cs`。Endpoints 处理 HTTP 和权限；Service 负责业务校验、LINQ、数据库写入与事务；实体描述数据库关系，DTO 决定客户端能收发哪些字段。修改业务校验时找 Service，增加返回字段时同时看 DTO 与查询投影。

`record` 是表达请求/响应数据的简洁类型。`Guid` 对应公开 UUID，`long` 对应内部 bigint，`decimal` 保存金额，`DateTime` 保存 UTC 业务时间；界面负责按日本时区显示。可空类型后的 `?` 提醒你处理“还没有付款时间”等情况。

`ShipRequest` 等请求 record 由 ASP.NET Core 的 JSON 反序列化创建；`ListQuery` 由 `[AsParameters]` 从查询参数绑定。源码没有 `new ShipRequest(...)` 并不代表类型无用。类型上的 `SuppressMessage` 只告诉 Rider 忽略这项误报，不影响运行时验证。普通未使用代码仍应检查和删除。

## 2. LINQ 如何变成 SQL

观察 `AsNoTracking → Where → OrderBy → Select → PageAsync`。`Where` 只描述查询，直到 `ToListAsync`、`CountAsync` 等才发送 SQL。只读查询不跟踪实体，投影只取 DTO 所需字段。导航属性用于表达关系，本身不会自动查询；本项目也没有启用懒加载。

尽量先在数据库查询上排序，再构造响应 record。部分 `new XxxDto(...)` 之后的属性排序无法被 EF Core 翻译成 SQL；这类错误只有实际执行查询才会暴露，编译成功不能替代数据库集成测试。商品排序与 Dashboard 都有对应实例。

尝试改变商品关键字、分类、排序、页码；在 PostgreSQL 用 `EXPLAIN (ANALYZE, BUFFERS)` 对比对应 SQL。排序包含稳定的次级键，避免相同价格在分页时随意跳动。不同页之间发生新写入时，OFFSET 分页仍可能移动；学习大数据翻页时再对照 `db/09_sample_queries.sql` 的 keyset 示例。

## 3. 为什么前端不能传最终价格

阅读 `Features/Checkout/Pricing.cs`与 `CheckoutService.cs`。客户端提供的是地址、配送方式和优惠码；服务器查询商品、库存与优惠，计算总价并签发短期报价。下单还会重算，避免前端改价格或拿过期优惠下单。

`async/await` 在等待数据库时让出执行线程，并不自动并行执行多个查询。一个 DbContext 不要同时跑多个数据库操作。`CancellationToken` 把浏览器取消请求传给后续等待。

## 4. 事务保护一次完整业务

`await using var tx = ...BeginTransactionAsync()` 开始事务；`SaveChangesAsync` 把实体变更发送到数据库，`CommitAsync` 才提交整个业务。下单同时创建订单、订单项、地址快照、核销、库存预占和状态记录，其中一步失败就回滚。

`Common/RowLocks.cs` 用参数化 SQL 锁定关键行。所有写入按约定顺序取锁，库存数量仍受数据库 CHECK 约束。`Idempotency-Key` 标识一次下单意图；同一个客户重试同一个键只返回同一个订单。再次下单应换新键。

## 5. 乐观并发和状态机

后台两个窗口同时修改商品时，`Data/StoreDbContext.Concurrency.cs` 把 PostgreSQL `xmin` 映射成版本。提交旧版本会得到 409，需要重新加载，不会静默覆盖别人刚保存的内容。

订单只能按允许的状态执行配货、发货、签收。发货还要同时释放预占、扣减实物库存、记录流水和物流。退款先锁定支付行，累计待处理和已完成退款不能超过收款金额。角色权限和数据归属在服务端校验，隐藏按钮只为改善体验。

## 建议练习顺序

1. 在 Rider 中对商品查询打断点，查看请求参数、LINQ 和返回 DTO。
2. 用商城注册自己的客户、保存地址、加入购物车，观察数据库新增记录。
3. 下单后先模拟失败再成功付款，在后台配货、发货、签收，回商城提交评价。
4. 两个后台窗口编辑同一个商品，体验版本冲突；使用只读账号验证接口拒绝写入。
5. 运行 `scripts/test_api.py`，阅读其中并发重放下单与退款上限检查，再尝试增加自己的业务测试。

模拟客户资料不是生产资料；本地测试也不要修改历史 seed 哈希来“绕过登录”。创建自己的账号即可。
