# ASP.NET Core 10 + EF Core 10 接入

> 当前仓库已完成接入，真实入口是 `Data/StoreDbContext.cs`，实体已按功能放入 `Features/*`。下面保留最初的 database-first 练习步骤，适合在独立练习项目中执行；不要在本仓库重新安装依赖、重复注册 DbContext 或用 scaffold 覆盖现有代码。阅读现有服务请从 [后端学习路线](backend/learning-guide.md) 开始。

现有项目目标是 `net10.0`，数据库已经建好。使用 Npgsql 的 EF Core 10 provider，先做 database-first reverse engineering。官方文档：[Provider](https://www.npgsql.org/efcore/)、[10.0 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)。

## 1. 添加同主版本依赖与本地工具

以下是下一步命令，本任务尚未改动 API 依赖或生成实体：

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version '10.*'
dotnet add package Microsoft.EntityFrameworkCore.Design --version '10.*'
dotnet new tool-manifest
dotnet tool install dotnet-ef --version '10.*'
```

如果已有 `.config/dotnet-tools.json`，跳过创建 manifest，用 `dotnet tool update dotnet-ef --version '10.*'` 更新已有工具。保持所有 Microsoft.EntityFrameworkCore 包为同一个 10.x 补丁版本。

## 2. 用环境变量提供连接，不写入源码

已验证本机 socket 参数：

```bash
export ConnectionStrings__EcommerceLab='Host=/tmp;Port=5432;Database=ecommerce_lab;Username=aminoas'
```

若换 TCP/另一用户，改为 `Host=localhost` 和对应角色，并从 shell 交互或 .NET user-secrets 提供密码。连接密码不应出现在 Git、日志或复制出来的命令中。此处已验证的本机 socket 连接未提供密码，不代表其他机器或 TCP 无需认证。

在 Program.cs 中注册（安装 provider 后）：

```csharp
using Microsoft.EntityFrameworkCore;
using mini_store.Data;

// 放在 builder.Build() 前。
builder.Services.AddDbContext<EcommerceLabContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EcommerceLab")));
```

先 scaffold 出 Context 再添加上述 using/注册，避免构建时类型尚不存在。scaffold 的 Name= 连接参数从应用配置读取环境变量，无需在命令中放密码。

```bash
dotnet ef dbcontext scaffold Name=ConnectionStrings:EcommerceLab \
  Npgsql.EntityFrameworkCore.PostgreSQL \
  --context EcommerceLabContext --context-dir Data --output-dir Models \
  --schema account --schema catalog --schema inventory --schema sales \
  --schema payment --schema shipping --schema marketing --schema review \
  --no-onconfiguring
```

不要加 `--force` 覆盖自己已修改的实体。SQL 命名是 snake_case，scaffold 会生成 C# 命名并映射具体表/列和 schema，无需额外命名插件。

## 3. 第一个只读 API / LINQ

```csharp
app.MapGet("/api/customers/{id:long}/orders", async (
    long id, EcommerceLabContext db, CancellationToken ct) =>
    await db.Orders.AsNoTracking()
        .Where(o => o.CustomerId == id)
        .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
        .Take(20)
        .Select(o => new { o.PublicId, o.OrderNumber, o.Status, o.GrandTotal })
        .ToListAsync(ct));
```

上面是学习内部 ID 的第一步。正式对外端点使用客户 public_id 并校验当前登录用户权限，不能只凭路径 ID 读取其他人的订单。Scaffold 生成的 DbSet/实体名称以实际文件为准。

`numeric` 映射 decimal；bigint 映射 long；uuid 映射 Guid；timestamptz 用 UTC DateTime/零偏移 DateTimeOffset；birth_date 用 DateOnly。将 EF 执行 SQL 打印到开发日志并与 `09_sample_queries.sql` 的组合索引查询对比，禁止开启包含凭据或敏感数据的生产日志。

## 4. 事务、并发与迁移

- 订单头、明细、优惠券、库存写入应在同一数据库事务，异常回滚；支付网关调用无法参加本地事务。
- 更新库存不能“先 SELECT 数量，再无条件 UPDATE”；练习调用已有 `inventory.reserve_stock`，或用条件 UPDATE + affected rows。
- 可把 PostgreSQL `xmin` 映射为 uint 并配置 `.IsRowVersion()`，练习 EF 乐观并发冲突；当前未给所有表增加冗余 version 列。
- 默认生成 UUID 位于数据库端；不要让 EF 客户端另生成一套 ID 规则覆盖数据库默认。
- 现有库由 SQL 初始化，scaffold 不会自动建立 EF migration 历史。不要直接对已有表运行第一份全量建表 migration；学习 migration 时先在单独的练习副本验证 baseline，再让后续变化由 migration 管理。
- 3 个 View 是只读学习入口，可映射为 keyless entity；不要把 View 当成可写表。
