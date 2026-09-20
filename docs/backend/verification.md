# 后端与前后台联调验证

以下按阶段记录实际执行结果；日期采用日本时间。

## OpenAPI 与 Swagger UI 验证（2026-09-21）

- 61 个业务/系统接口全部具备唯一 WithName、中文 WithSummary、WithDescription、分组及对应的错误响应声明；成功响应保留实际的 200/201/204 和 DTO。
- 20 种请求 DTO 的 71 个字段增加 XML 参数说明；已从运行时文档读取确认生成结果。手动读取的 Idempotency-Key 请求头显式标注为必填。
- JWT 安全声明从真实 RequireAuthorization 元数据生成：50 个受保护接口、11 个公开接口；公开注册/登录不要求预先持有令牌。
- Program 注册内置 OpenAPI 服务，在 Development 启用 Swagger UI，地址为 `/swagger`。只增加 Swashbuckle.AspNetCore.SwaggerUI 组件，页面读取原 `/openapi/v1.json`，没有引入另一套契约生成器。
- 构建零警告、零错误；现有 72 项 HTTP 检查和隔离库 SQL 审计通过，并增加所有操作摘要、参数、授权、响应、请求字段及 Swagger HTML/脚本的断言。
- 浏览器确认中文分组、下单幂等请求头、200/201 与错误模型、JWT 授权框可见。原本地服务已重启到新版本；未通过 Swagger 执行实际下单等写操作。
- 教程同步加入包引用、服务注册、Swagger 中间件与调试步骤；重新提取商品入门代码后，构建、商品读取、错误响应与 Swagger 页面均通过。

## 中文注释与从零搭建教程验证（2026-09-21）

- 12 个业务模块均新增 README，补齐文件职责、调用流程、接口权限、关系与规则、SQL 和练习；根目录新增完整搭建教程与学习导航。
- 补充 Service、DTO、导航属性、Program、DbContext 和关键事务的简体中文说明。在追加 OpenAPI 配置之前，与本轮注释修改前的 72 个 C# 源文件逐一比较语法 token，业务代码一致，仅注释和排版改变。
- 当前项目构建零警告、零错误，CSharpier 检查 73 个文件通过。
- 在只读事务中执行 12 个模块的 SQL 及教程商品查询，共 13 条，全部成功。核对时发现幂等表没有 id 列，已将文档排序改为实际复合键 customer_id、request_key。
- 从教程提取完整代码，在独立临时目录实际构建并运行 Hello World 和商品查询阶段；健康检查、分页、真实 UUID 详情、400/404 与 OpenAPI 均通过。
- 按教程第 9 节导入完整源码后再次构建，在随机隔离测试库通过 72 项 HTTP 检查及 SQL 审计；测试库由脚本清理。现有 ecommerce_lab 只执行读取，没有 reset 或重播 seed。
- 教程及模块文档的本地链接已检查，模块接口与 OpenAPI 对照无遗漏；两个应用级入口在 Features 导航说明。尚未在 Windows 上执行教程命令。

## Feature-first 重构验证（2026-09-20）

- 实体迁入所属 `Features` 目录，12 个功能均有独立 Service 和 DTO；共享 DbContext 的表、列及关系映射保持不变。Endpoints 不再直接查询数据库，Service 不读取 HTTP 上下文或返回 HTTP Result。
- `dotnet build` 零警告、零错误；CSharpier 检查 73 个文件通过。
- `scripts/test_api.py` 通过 72 个真实 HTTP 检查及隔离库 SQL 审计。新增具名 DTO、OpenAPI、资料、购物车和排序回归；不同税率商品验证按实际含税价排序。
- 与旧服务对照：61 个接口的路径、方法、请求体与参数定义保持一致；7 组公开查询的 JSON 响应逐值一致。重启后的本地中型数据集上，价格升降序与评分排序检查通过。
- OpenAPI 快照已从重启后的服务导出，包含 80 个 schema；创建及无响应体操作明确标注 201、200、204。
- 回归时修复了 record 投影后排序无法翻译为 SQL 的问题：Dashboard 先排序再投影；商品先按 SQL 含税价或评分排序再投影。不把全量商品加载到内存排序。
- Rider：对 JSON / 查询参数绑定与 EF 查询自动实例化的类型，使用局部 `SuppressMessage` 并注明原因；未关闭项目级未使用代码检查。编译与 HTTP 绑定已验证，未声称运行了 Rider 全项目检查。
- 配套商城搜索框使用整个容器的焦点边框；浏览器确认输入焦点与 Tab 到按钮的提示均可见。商城构建与现有 8 项测试通过。

本次只在隔离库写入测试数据，本地 `ecommerce_lab` 未 reset。以下是初次联调时的历史记录，表行数不是本次重新统计。

## 初次联调记录（重构前）

### 自动验证

- C#：CSharpier 检查 50 个文件通过；`dotnet build` 零警告、零错误。
- 后端：`scripts/test_api.py` 在独立随机数据库通过 60 个 HTTP 检查，并通过 SQL 全量一致性审计。覆盖管理员/客户隔离、只读权限、资源归属、商品版本冲突、默认地址、报价/下单、同键并发重放、支付失败/成功/重复调用、取消、发货签收、评论审核、并发超额退款保护和各后台查询。
- 原库：`db/08_verify.sql` 全部规则异常数为 0；随后执行 ANALYZE。验证包括外键、金额、优惠券核销、退款总额、库存/预占与流水余额、时间线和状态历史。
- 中文注释：增量表、字段及原有表/视图共 269 个字段，269 个具有说明；幂等 comments 命令执行通过。
- 商城：5 个展示映射/交互测试通过，正式构建通过。
- 后台：请求适配测试、TypeScript 类型检查与正式构建通过；本次修改的业务页面/请求层/路由通过现有 ESLint 检查。

### 浏览器交叉验证

在本机浏览器真实完成注册、登录、地址保存、商品规格选择、加购、报价、下单、模拟付款，然后切换后台处理同一笔订单，配货、发货、签收，再回商城评价、后台审核发布，最后确认商品详情中可见评价。

- 订单：`MS-20260920-ae3d9c42b9b44f0a9e243c6`。
- 商品：Nami Works 調光デスクライト 結-065，Natural，数量 1。
- 税前 ¥4,500 + 税 ¥450 + 运费 ¥500 = ¥5,450；商城、后台和数据库一致。
- 状态：pending → confirmed → paid → processing → shipped → delivered。
- 商品可售量从 142 变成 141。库存流水：[('reservation', 1), ('release', -1), ('sale', -1)]。
- 物流：Yamato，教学单号 QA20260920130301；商城显示相同的发货和签收时间。
- 评价：提交后待审核，后台发布后商品页公开显示，重复评价按钮被禁用。
- 临时联调客户与临时管理员已停用；订单/流水/评价保留用于观察。可用学习管理员与只读账号仅保存在本机 `.local/learning-accounts.json`，不进入 GitHub。也可自己运行 `scripts/create_admin.py` 创建新账号。

### 联调中修复的问题

修复了 EF 部分唯一索引误推断一对一导航、xmin 原始 SQL 映射、可选查询参数绑定、带时区优惠券日期、预占流水时间早于下单时间、缺失的前端 DTO 字段、账户资料入口与旧 Mock 测试。开发服务器修改路由模块时出现的模块加载失败经重新加载后恢复；实际验证页面正常加载和业务流转。格式化后再次构建及检查，修复了一个 TypeScript 请求体类型推断问题。

### 数据库规模

PostgreSQL 18.6，数据库 ecommerce_lab，8 个领域 schema，25 张表，3 个视图，约 614 MB，总计 2,591,539 行。规模会随学习操作继续变化；现有历史 seed 未 reset。

| 表 | 行数 |
|---|---:|
| account.admin_users | 3 |
| account.customer_addresses | 30,001 |
| account.customers | 20,001 |
| catalog.brands | 150 |
| catalog.categories | 90 |
| catalog.product_categories | 6,000 |
| catalog.product_images | 5,000 |
| catalog.product_variants | 12,000 |
| catalog.products | 5,000 |
| inventory.stock_movements | 940,375 |
| inventory.stocks | 48,000 |
| inventory.warehouses | 4 |
| marketing.coupon_redemptions | 22,808 |
| marketing.coupons | 12 |
| payment.payments | 103,427 |
| payment.refunds | 5,487 |
| review.product_reviews | 63,642 |
| sales.cart_items | 29,010 |
| sales.carts | 11,667 |
| sales.checkout_requests | 1 |
| sales.order_addresses | 200,002 |
| sales.order_items | 315,103 |
| sales.order_status_history | 580,079 |
| sales.orders | 100,001 |
| shipping.shipments | 93,676 |

## 验证边界

浏览器验证覆盖主要成交流程与页面展示；并非对每种筛选组合和所有设备逐项穷举。真实支付网关、物流承运商、邮件、生产部署和高负载压测未接入或执行。收藏仍为浏览器本地功能，商品图片使用示意图。JWT 有效期 30 分钟，没有刷新令牌；这是明确的本地学习范围。
