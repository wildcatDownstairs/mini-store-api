# Auth：登录与权限

[返回模块学习导航](../README.md)

负责客户注册/登录、后台登录以及 JWT 认证策略。客户与管理员使用不同表；管理账号通过项目脚本显式创建，不提供公开后台注册入口。

## 文件怎么读

先看 `AuthEndpoints.cs` 的入口，再读 Service 对应方法，对照 DTO 与实体。共享上下文见 [StoreDbContext](../../Data/StoreDbContext.cs)，公共分页与校验见 [RequestRules](../../Common/RequestRules.cs)。

| 文件 | 职责 |
|---|---|
| [AdminUser.cs](AdminUser.cs) | EF Core 实体，表列和关系映射统一配置在 Data/StoreDbContext.cs。 |
| [AuthConfiguration.cs](AuthConfiguration.cs) | JWT 认证与角色策略，动态检查账户状态。 |
| [AuthDtos.cs](AuthDtos.cs) | 请求与响应结构；对外 UUID、金额与空值语义见注释。 |
| [AuthEndpoints.cs](AuthEndpoints.cs) | HTTP 路由、请求绑定、授权与响应状态。 |
| [AuthService.cs](AuthService.cs) | 本模块查询、校验、业务规则和事务。 |

## 一次请求如何经过本模块

`AuthEndpoints.MapAuth → AuthService.RegisterAsync / LoginAsync → TokenResponse`。之后浏览器发送 `Authorization: Bearer <accessToken>`；`AuthConfiguration` 校验令牌并查询账号当前状态，再把内部 ID 放进当前请求的 ClaimsPrincipal。

## 接口与权限

下表路径占位符使用公开 UUID（slug 除外）；列表参数仅在对应 Service 中实现时才生效，不是所有列表都支持 ListQuery 的所有字段。

| 方法 | 路径 | 调用身份 |
|---|---|---|
| POST | `/api/admin/auth/login` | 无需登录 |
| POST | `/api/auth/register` | 无需登录 |
| POST | `/api/auth/login` | 无需登录 |

## 数据库关系与业务规则

`account.customers` 保存客户；`account.admin_users` 保存独立后台账号。两表各自使用 lower(email) 唯一索引，防止大小写差异产生同类重复账号。

- 密码先经 PasswordHasher 哈希再存储。种子客户的假 bcrypt/argon 风格字符串无法登录；学习时注册自己的客户。
- JWT 有效期 30 分钟，包含公开 UUID、角色与账号类型，不包含密码。JWT 签名验证不意味着其内容是保密的。
- Customer 策略限定客户；AdminRead 允许 operator 和 viewer；AdminWrite 只允许 operator。
- 每次认证读取当前数据库账号状态和管理员角色，所以停用后的旧令牌无法继续使用。
- 本地学习版注册后直接 active，但不会伪造已验证邮箱；当前没有邮件验证、刷新令牌或密码重置流程。

## 这里可以学到什么

依赖注入会把 DbContext 和 IConfiguration 传给服务。`ClaimsPrincipal` 表示已识别的调用人，`ActorId()` 读取认证阶段加入的内部 ID。认证失败通常为 401；身份有效但没有权限为 403。公开 UUID 不能代替授权检查。

## 只读 SQL 练习

在 Rider 或 psql 连接 `ecommerce_lab` 后执行。结果随你的学习操作变化，空结果不一定是错误。

```sql
SELECT public_id, display_name, role, is_active, created_at
FROM account.admin_users
ORDER BY created_at DESC, id DESC
LIMIT 5;
```

只选择学习需要的列，避免 `SELECT *` 把 password_hash 等内部字段带到展示结果。

## 建议动手顺序

阅读 `AuthConfiguration.AddStoreAuth` 中的三项策略，再对比使用客户和后台只读身份调用 `/api/admin/orders` 的结果。创建账号方式见根 README，不修改种子哈希绕过登录。

完整隔离业务测试从项目根目录运行 `.venv/bin/python scripts/test_api.py`；它会创建并清理自己的测试库，不重置 `ecommerce_lab`。API 启动与账号准备见 [项目 README](../../README.md)。

相关模块：[Customers](../Customers/README.md)、[Orders](../Orders/README.md)。
