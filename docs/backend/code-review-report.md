# Mini Store ASP.NET Core Code Review 报告

审查依据是 [code-review-prompt.md](code-review-prompt.md)。阅读范围覆盖 `Program.cs`、`Common/`、`Data/`、12 个 `Features` 模块、支付与库存相关 SQL，以及 GitHub 构建工作流。

## 结论

这个 API 可以继续作为学习服务使用。客户与管理员身份是分开的，下单、预占、取消、发货和退款都放在明确的事务里，金额由服务端重算。这次确认了 4 个中低优先级缺陷并已修复，另外收紧了登录耗时差异、客户端断开时的错误分类，以及会让 CI 无法编译的目标框架。

## 已修复

### 缺失的布尔值和商品版本会走 CLR 默认值

`SimulatedPaymentRequest.Success`、`RefundDecision.Approved`、`CouponStatusRequest.IsActive` 在 JSON 里省略时会变成 `false`。结果分别是记一笔失败支付、拒绝一笔待审核退款、停用优惠券。`ProductStatusRequest.Version` 省略时会变成 `0`，随后以版本冲突返回 409。OpenAPI 已把这些字段标成必填。

这些字段增加了 `JsonRequired`。省略时返回 400 和「请求格式或参数不正确。」显式传入 `false` 或 `0` 仍然按原业务处理。没有全局打开构造参数必填，因为创建商品时 `version` 本来就可以省略。

### 搜索词里的 `%`、`_` 和 `\` 被当成通配符

商品、订单、客户、库存、支付、物流、优惠券和评价的关键词都拼进 `ILIKE '%关键词%'`。PostgreSQL 的 `ILIKE` 默认把 `%`、`_` 当通配符，反斜杠当转义符。查询 `q=_` 会命中所有非空名称。

`Rules.ContainsPattern` 先转义这三个字符，再加两侧百分号。集成测试断言 `q=_` 不再返回商品。

### 构建工作流使用 .NET 8，并且执行不存在的测试项目

`global.json` 和项目目标框架是 .NET 10.0.401。`.github/workflows/dotnet.yml` 安装的是 `8.0.x`，随后执行 `dotnet test`。这个仓库没有测试项目，HTTP 检查在 `scripts/test_api.py`。工作流改为按 `global.json` 安装 SDK，只做 restore 和 build。

### 登录可以用耗时区分邮箱是否存在

账号不存在时，原来的条件判断不会调用 `PasswordHasher`。真实账号会做一次 PBKDF2。现在无论客户或管理员是否存在，都对真实哈希或固定占位哈希做一次相同成本的校验。占位哈希不会入库；只有数据库中的 `AQAAAA` 哈希校验通过才算密码正确，因此知道占位符原文也不能登录。种子假哈希同样走占位校验。

### 顺手收紧的两处

客户端已经断开请求时，异常处理器直接结束，不再把取消记成 500，也不再尝试写响应。

订单号以前先生成一个完整 GUID，保存前又生成另一个并截断。现在只生成一次，并在写入前截到 35 字符，仍低于数据库 40 字符上限。

概览里的客单价改为 `MidpointRounding.AwayFromZero`，与行税额使用的四舍五入一致。

## 核对通过

- JWT 校验发行者、受众、签名、5 秒时钟偏移和有效期。每次请求按 `kind` 重读客户或管理员，停用、删除后替换角色；内部主键只放在当次请求的 `db_id`，不写进令牌。
- `/api/me` 与 `/api/admin` 的读写策略和资源归属是分开的。客户读取他人订单得到的是 404。只读管理员不能创建商品。
- 报价令牌由 Data Protection 保护并限制 10 分钟。下单时重新读取购物车和地址，摘要不一致返回 409。价格来自数据库，不来自请求体。
- 下单事务的锁顺序是客户、商品、规格、优惠券、库存。同一客户的并发重放会等到客户行锁，再返回原订单。库存预占调用 `inventory.reserve_stock`，仓库选择要求单仓能满足全部规格。
- 取消只接受未付款的 `pending` 或 `confirmed`，并按预占流水释放。发货要求已付款且状态为 `processing`，出库仓必须等于预占仓。退款申请把待审核和已完成金额一起计入额度，数据库触发器再检查一次。
- `FromSql` 使用插值参数。唯一约束、外键和数值约束的数据库错误会转成 409 或 400，响应不含 SQL。连接串由 `NpgsqlConnectionStringBuilder` 组装，`IncludeErrorDetail` 为 false。
- 食品与非食品税率、10% 折扣、标准配送和运费门槛与集成测试中的期望金额一致：税前 2000、折扣 200、税额 180、运费 500、应付 2480。

## 残余风险

- 概览按 `PaidAt.Date` 分日。`timestamptz` 转日期使用数据库会话时区。学习库若固定在 `Asia/Tokyo` 或 `UTC`，结果是稳定的；换服务器时区会让日界线移动。这次没有改查询，避免在没有时区约定的情况下改动已有图表。
- 下单时用 `FOR UPDATE` 载入的库存实体仍被 EF 跟踪。随后的 `reserve_stock` 直接更新数据库。当前代码不再修改这些实体，`SaveChanges` 不会把旧预占量写回去。以后如果在同一上下文里改动已跟踪的 `Stock`，需要先分离实体或重新读取。
- 购物车同时返回按件四舍五入的含税单价，以及按整行计税的 `lineTotal`。数量大于 1 时，单价乘数量可能不等于行金额。订单收取的是行金额，这与「税按行四舍五入」一致。
- 商品列表的含税价在 SQL 里使用 `Math.Round`。PostgreSQL `numeric` 的 `round` 是四舍五入。若补上 C# 的 `MidpointRounding` 参数，要先确认 EF 仍能把表达式翻译成 SQL。
- 登录和注册的 429 按连接上的远程地址计数。部署到反向代理后面时，需要单独决定转发头策略；当前只读取连接地址，调用方不能靠伪造转发头绕过限流。
- 优惠券只有全局次数，没有每客户一次的限制。表结构也没有这个字段，所以这次不在服务里发明新规则。
- `ApiError.Code` 里的机器可读代码没有放进响应。公司契约里的 `code` 是 HTTP 状态，这次保持该契约。

## 验证

- `dotnet csharpier check .`：75 个文件通过。
- `dotnet build`：0 个警告，0 个错误。
- 在 PostgreSQL 16.15 上运行 `PGHOST=/var/run/postgresql python3 scripts/test_api.py`：105 项 HTTP 检查通过，隔离库 SQL 审计通过。随机测试库已删除。该环境没有 PostgreSQL 18 的 `uuidv7()`，脚本会退回 `gen_random_uuid()`。
- 未连接数据库时启动服务：`POST /api/auth/login` 空对象、缺少密码、非法 JSON 都返回 400 和公司响应外壳；字段齐全的登录请求会进入服务并在数据库不可用时返回 500。`GET /api/config` 返回 200，`pageSize=0` 返回 400。
