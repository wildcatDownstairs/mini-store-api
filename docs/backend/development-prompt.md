# Mini Store 后端实施提示词

基于本地商城、Vuexy 管理系统及 ecommerce_lab 真实 PostgreSQL 数据库，实现供 C# 后端初学者阅读的 ASP.NET Core 服务。

## 技术与组织

.NET 10 LTS、C# 14、EF Core 10、Npgsql。单个可运行 Web 项目，按 Features/Auth、Catalog、Customers、Cart、Checkout、Orders、Inventory、Payments、Shipping、Marketing、Reviews、Dashboard 划分业务。每个业务放自己的端点、请求 DTO 和有必要的服务；DbContext 与数据库实体集中在 Data。不要通用 Repository、MediatR、CQRS、微服务或消息队列。

代码的公开类型、业务步骤和容易误解的 C# 语法使用简体中文解释；重点讲 record、依赖注入、async/await、LINQ 延迟执行、AsNoTracking、DTO 投影、事务、行锁和乐观并发，而不是逐字翻译每条赋值。

## 业务验收

- 商城：商品搜索/筛选/稳定分页/详情/评价、品牌/分类、注册登录、个人资料与地址、购物车、服务端报价、幂等下单、订单历史/详情、取消、模拟支付与购买评价。
- 管理端：独立管理员登录与运营/只读权限、概览、商品创建编辑上下架与规格、客户详情与停用、库存列表/调整/流水/仓库、订单确认/配货/发货/签收、支付查询/退款申请审核、优惠券维护、评价审核。
- 两套前端全部业务读写对接 HTTP API；保留设计与 UX，不再用 localStorage 模拟订单、库存和权限。仅本地收藏可保留。分页与筛选交给服务端。联调验证商城下单后管理端可处理、库存和订单状态跨端一致。
- 最后在当前 GitHub 账号建立 mini-store-api、mini-store-admin、mini-store-web 三个公开仓库并推送；不得提交凭据、非模拟个人数据、venv、node_modules 或编译产物。
- API 使用 UUID；对没有 public_id 的地址和订单项增量补充 UUID。保留 bigint 外键；订单名称、单价、地址为快照。
- 保留 23 张现有表与数据。增量增加管理员身份表、幂等下单请求表；每个新字段和新表必须有简体中文 COMMENT。
- 身份使用密码哈希与短期 Bearer Token；管理员身份与客户隔离，服务端校验客户归属、管理员角色和停用状态，种子假哈希禁止登录。不创建公开默认密码。开发管理员通过显式命令与环境变量创建。
- 不相信前端价格。JPY 整数计价；食品 8%、其他 10%，折扣按行分摊，税按行四舍五入，满额配送政策明确。
- 下单在单一事务内重算并验证报价、锁定客户/购物车/优惠券/库存、创建快照/核销/预占/状态历史/幂等结果。库存使用现有 reserve_stock 函数；首版每个订单选择可完整履约的单仓，不将跨仓库存总量当成单仓库存。
- 取消只支持未付款订单，释放预占；优惠核销保留审计与已用次数，明确不返还额度。
- 只提供 Development 环境显式开启的模拟支付/退款结算，不实现虚假的网关 webhook。不采集银行卡信息。发货原子出库并记录流水；退款不自动将商品入库。
- 查询上限、白名单排序、输入校验、ProblemDetails、参数化 SQL、CancellationToken、CORS 明确来源、登录限流。库存与退款并发写入需要真实 PostgreSQL 验证。
- 不在源码、日志或 Git 写密码/密钥；连接优先使用 PG* 环境变量，也支持 ConnectionStrings__EcommerceLab。

## 交付

实际 restore/build/run；真实数据库上的 HTTP 集成测试覆盖鉴权、越权、下单重放/冲突、并发库存、订单金额、发货、退款上限、审核和只读角色。可重复测试使用独立命名的实验数据库，不清空用户数据。提供一键运行、非破坏性增量升级、OpenAPI、HTTP 示例、中文学习路线和验证报告。失败必须修复，边界如无真实支付、前端仍 mock 要明确。
