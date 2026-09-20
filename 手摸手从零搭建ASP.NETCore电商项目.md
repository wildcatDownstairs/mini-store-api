# 手摸手从零搭建 ASP.NET Core 电商项目

这份教程用本仓库的真实电商数据库和功能实现，带你从空项目走到可以运行的后端。你会先亲手写一个商品查询模块，理解四类文件如何合作，再逐个接入完整电商功能。

适合刚学 C#、ASP.NET Core 和 PostgreSQL 的读者。前半段只读现有数据库；后半段可通过 API 创建自己的学习账号和测试订单。**不要在现有项目里执行 `dotnet new`，不要 DROP 或 reset 现有数据库。**

教程按 macOS 的终端命令编写。Windows 可在 Rider 终端采用等价 PowerShell 命令，Python venv 路径由 `.venv/bin/python` 换为 `.venv/Scripts/python.exe`。

已在本机验证 Hello World、只读商品模块和完整功能导入三个阶段；最后一阶段通过 72 项 HTTP 检查与隔离库 SQL 审计。具体范围见 [验证报告](docs/backend/verification.md)。

## 路线与完成标准

| 阶段 | 你会得到什么 | 检查点 |
|---|---|---|
| 1 | 空 ASP.NET Core 项目 | 浏览器能看到 Hello World |
| 2 | 真实 PostgreSQL 查询 | `/health` 能连库，商品接口有数据 |
| 3 | Feature-first 商品模块 | Endpoints → Service → EF Core → DTO |
| 4 | 完整商品管理与认证 | 后台写操作需要合法角色，过期版本返回冲突 |
| 5 | 全部电商业务 | 登录、地址、购物车、报价、下单、支付、出库、评价 |
| 6 | 可验证的工程 | 构建、格式化、独立测试库中的业务闭环通过 |

每一阶段先跑通再继续。后半段会**明确导入本仓库经过验证的完整功能源码**，不是让你凭几个省略片段补出支付和库存系统；导入后按模块 README 逐段阅读、打断点和修改。

## 1. 先分清几个名称

- **C#** 是编程语言。本仓库使用 C# 14。
- **.NET SDK** 提供编译、运行、建项目等工具；本仓库是 .NET 10，`global.json` 固定 SDK 10.0.401，并允许同版本更新的 patch。
- **ASP.NET Core** 是 .NET 的 Web 框架，接收 HTTP 请求并返回 JSON。
- **EF Core** 把 C# 查询和实体变更翻译为数据库操作；**Npgsql** 是 PostgreSQL 驱动及对应 EF provider。
- **PostgreSQL** 保存真实数据，并执行约束、索引、SQL 和事务。API 停止后，数据库数据仍然存在。

它们不是同一个版本号体系，不要把“C# 14”写成“.NET 14”。以下包版本取自本项目的可运行配置，不代表需要自行追逐每个包的最新版本。

## 2. 准备环境与参考源码

终端检查：

```bash
dotnet --info
dotnet --list-sdks
psql --version
python3 --version
```

必须有 **SDK**，仅安装 .NET Runtime 不够。若这台 Mac 已安装 SDK 但找不到 dotnet，可先在当前终端设置：

```bash
export PATH="$HOME/.dotnet:$PATH"
```

Rider 打开项目时会读取 `.csproj` 和 `global.json`。SDK 不匹配先安装对应版本，不要通过随意改 target framework 或忽略错误来继续。

本机参考源码路径如下；其他机器先取得包含本教程的完整仓库版本，再改成自己的路径：

```bash
export REFERENCE_PROJECT="/Users/aminoas/Downloads/WebApplication/mini-store"
export LEARNING_PROJECT="$HOME/Projects/mini-store-learning"
```

`REFERENCE_PROJECT` 只用来读取已完成的源码，`LEARNING_PROJECT` 必须是新目录。本文后续命令使用这两个变量；换终端后要重新设置。参考仓库是 [mini-store-api](https://github.com/wildcatDownstairs/mini-store-api)，代码说明入口是 [模块导航](Features/README.md)。

## 3. 创建空 Web 项目，先听见第一声 Hello

```bash
mkdir -p "$HOME/Projects"
dotnet new web --name mini-store --framework net10.0 --output "$LEARNING_PROJECT"
cd "$LEARNING_PROJECT"
```

如果目标目录已经有工程，换一个新目录，不要加 `--force`。在 Rider 用 Open 打开新目录下的 `mini-store.csproj`。

将新项目 `Program.cs` 写成下面的完整内容：

<!-- tutorial-file: hello/Program.cs -->
```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello, Mini Store!");

app.Run();
```

```bash
dotnet run --no-launch-profile --urls http://127.0.0.1:5284
```

浏览器访问 `http://127.0.0.1:5284/`，应看到 `Hello, Mini Store!`。5284 与现有后端 5274 分开，避免端口冲突。停止服务按 Ctrl+C；终端被服务占用时，检查请求可另开一个终端：

```bash
curl -i http://127.0.0.1:5284/
```

`CreateBuilder` 准备配置与服务，`Build` 构造应用，`MapGet` 注册路由，`Run` 开始监听。注册路由时不会立即执行处理请求的 lambda。官方介绍见 [Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)。

## 4. 确认真实数据库可用

先选择连接方式。下面是本机 PostgreSQL socket 的配置；若使用 TCP，把 PGHOST 改为 `localhost`。PGUSER 填实际角色，不一定叫 postgres：

```bash
export PGHOST=/tmp
export PGPORT=5432
export PGUSER="$(whoami)"
export PGDATABASE=ecommerce_lab
psql -X -d postgres -c '\l'
psql -X -d ecommerce_lab -c 'SELECT current_database(), current_user, version();'
psql -X -d ecommerce_lab -c 'SELECT count(*) FROM catalog.products;'
```

通过 socket 连接时，服务器的 `inet_server_addr()` / `inet_server_port()` 可能为空，并不表示未连接成功。需要密码时通过交互或本机开发配置提供，不把真实密码写进本教程、源码或 Git。

### 已有 ecommerce_lab：本机走这一条

不要重新 seed/reset。确认原有表存在，并执行本仓库的受保护增量升级：

```bash
cd "$REFERENCE_PROJECT"
# 已有 .venv 时直接使用；没有才运行下两行创建并安装依赖。
python3 -m venv .venv
.venv/bin/python -m pip install -r seed/requirements.txt
.venv/bin/python scripts/upgrade_api.py
.venv/bin/python scripts/manage_db.py comments
cd "$LEARNING_PROJECT"
```

升级会检查实验库标识，只补 API 需要的结构，保留现有数据。不属于本任务管理的同名库会被拒绝；此时先核对来源，不要删除库来让命令“通过”。

### 新机器尚无 ecommerce_lab：仅首次执行

PostgreSQL 服务必须已启动，角色需要创建数据库权限。使用参考仓库现有脚本建立完整八个领域 schema，不手写简化表来绕过真实关系：

```bash
cd "$REFERENCE_PROJECT"
python3 -m venv .venv
.venv/bin/python -m pip install -r seed/requirements.txt
SEED=20260918 SEED_SCALE=small .venv/bin/python scripts/manage_db.py init
.venv/bin/python scripts/upgrade_api.py
.venv/bin/python scripts/manage_db.py comments
cd "$LEARNING_PROJECT"
```

先用 small 足够练习；已有 medium 数据就继续用，不为教程重新生成。基础脚本依次建立扩展、schema、表、约束、索引、视图和函数；`11_api.sql` 再补后台账号与幂等记录。**升级为完整 API 结构以后，不要再把 init 当作启动命令重复运行。**

## 5. 配置依赖，理解每个包的职责

在学习目录里，将 `mini-store.csproj` 替换为下面内容（完整文件，不是追加到旧 XML 后面）：

<!-- tutorial-file: read/mini-store.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>MiniStore</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="10.2.3" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.12" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

`Web` SDK 自动提供 ASP.NET Core 框架；Npgsql provider 提供 UseNpgsql 和查询翻译，Design 支持 EF 命令工具，OpenApi 导出契约，SwaggerUI 提供可交互接口页面，JwtBearer 为后续登录鉴权准备。`Nullable` 帮助发现空值问题，`ImplicitUsings` 提供常用命名空间。

```bash
cp "$REFERENCE_PROJECT/global.json" .
cp "$REFERENCE_PROJECT/.gitignore" .
cp "$REFERENCE_PROJECT/.editorconfig" .
cp "$REFERENCE_PROJECT/.csharpierrc.json" .
cp "$REFERENCE_PROJECT/dotnet-tools.json" .
dotnet restore
dotnet tool restore
```

这是一个使用现成 PostgreSQL schema 的 **database-first** 项目。此时不运行 `EnsureCreated()`，也不执行 `dotnet ef database update` 假装已经有迁移文件。库结构和 EF 映射都已存在，下一步直接建立对应关系。

## 6. 引入真实实体和映射，认识数据库与 C# 的边界

实体字段多，数据库已有完整中文注释。为避免初学者手抄漏掉约束与导航，先把参考项目的**实体和映射**导入；此时不会复制任何业务 Service 或 Endpoints。

在学习目录执行以下 Python 命令，读取前面设置的环境变量：

```bash
python3 - <<'PY'
import os
import re
import shutil
from pathlib import Path

reference = Path(os.environ['REFERENCE_PROJECT']).resolve()
learning = Path(os.environ['LEARNING_PROJECT']).resolve()
assert reference != learning, '必须使用独立学习目录'
for folder in ['Data', 'Common']:
    shutil.copytree(reference / folder, learning / folder, dirs_exist_ok=True)
for source in (reference / 'Features').rglob('*.cs'):
    # 本仓库实体使用 partial class；业务服务和 DTO 不匹配这个条件。
    if re.search(r'public partial class\s+\w+', source.read_text()):
        target = learning / source.relative_to(reference)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
print('实体、DbContext 和公共工具已导入；尚未引入业务路由。')
PY
```

为什么要导入其他模块的实体？因为 `Product` 导航会引用订单项、评价等类型，`StoreDbContext` 也统一配置全部关系；少复制一部分会产生找不到类型的编译错误。它们此时只是模型，不会自动执行业务。

在 Rider 依次打开：

1. `Features/Products/Product.cs`：`Id` 对应 bigint，`PublicId` 对应 UUID，`decimal` 对应 NUMERIC，`DateTime?` 表示可以尚无时间。
2. `ProductVariant.cs`：一个商品对应多个实际 SKU；订单通常买的是规格。
3. `Data/StoreDbContext.cs`：`ToTable("products", "catalog")` 指向真实 schema 和表，`HasColumnName` 连接 C# 属性和 snake_case 列。
4. `Data/StoreDbContext.Concurrency.cs`：`Version` 是映射到 xmin 的影子属性，不必写在 Product.cs 上。

导航属性是对象关系，不是数据库自动查询开关。`virtual`、`null!`、`partial` 的含义见 [基础语法对照](Features/README.md)。重做 scaffold 时应在独立练习目录比较生成结果，不用 `--force` 覆盖这里已经修正的映射。

## 7. 亲手写第一个 Feature：完整的商品只读接口

在学习项目 `Features/Products` 目录新增下面三个文件；实体 `Product.cs` 上一步已经导入。此处是**入门版接口**，返回税前基础价，暂不接现有前端。第 9 节将用完整版本替换这三个文件。

### 7.1 ProductDtos.cs：决定 API 返回什么

<!-- tutorial-file: read/Features/Products/ProductDtos.cs -->
```csharp
namespace MiniStore.Features.Products;

/// <summary>入门商品摘要：Id 为公开 UUID，BasePrice 为税前基础价。</summary>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string BrandName,
    decimal BasePrice,
    string Currency
);
```

不返回整个 Product 实体，否则可能暴露内部键、产生导航循环或返回过多数据。DTO 的 `Id` 是 `PublicId`，与实体的内部 `Id` 不同。

### 7.2 ProductService.cs：组合并执行真实数据库查询

<!-- tutorial-file: read/Features/Products/ProductService.cs -->
```csharp
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Products;

public sealed class ProductService(StoreDbContext db)
{
    public async Task<PageResult<ProductListItemDto>> ListAsync(
        ListQuery query,
        CancellationToken ct
    )
    {
        query.Validate();
        var source = db.Products.AsNoTracking()
            .Where(p => p.Status == "active" && p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = "%" + query.Q.Trim() + "%";
            source = source.Where(p => EF.Functions.ILike(p.Name, pattern));
        }

        return await source
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new ProductListItemDto(
                p.PublicId,
                p.Name,
                p.Slug,
                p.Brand.Name,
                p.BasePrice,
                p.Currency
            ))
            .PageAsync(query, ct);
    }

    public async Task<ProductListItemDto> GetAsync(Guid id, CancellationToken ct)
    {
        return await db.Products.AsNoTracking()
            .Where(p => p.PublicId == id && p.Status == "active" && p.DeletedAt == null)
            .Select(p => new ProductListItemDto(
                p.PublicId,
                p.Name,
                p.Slug,
                p.Brand.Name,
                p.BasePrice,
                p.Currency
            ))
            .SingleOrDefaultAsync(ct)
            ?? throw ApiError.NotFound();
    }
}
```

`Where` 过滤，`OrderBy` 排序，`Select` 只取 DTO 所需字段。导航字段 `p.Brand.Name` 可以在投影中翻译为关联 SQL，不需要先把全部 Brand 实体读进来。

`PageAsync` 来自导入的 `Common/RequestRules.cs`，内部先 Count，再 Skip/Take/ToListAsync。请求取消会通过 `ct` 继续传到驱动。不要对同一个 DbContext 使用 Task.WhenAll 并行跑多个查询；上下文不是线程安全的。[EF Core 上下文说明](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

### 7.3 ProductEndpoints.cs：把 URL 连接到服务

<!-- tutorial-file: read/Features/Products/ProductEndpoints.cs -->
```csharp
using MiniStore.Common;

namespace MiniStore.Features.Products;

public static class ProductEndpoints
{
    public static void MapProducts(this WebApplication app)
    {
        var group = app.MapGroup("/api/store/products").WithTags("商城商品");
        group.MapGet("", (
            [AsParameters] ListQuery query,
            ProductService service,
            CancellationToken ct
        ) => service.ListAsync(query, ct));

        group.MapGet("/by-id/{id:guid}", (
            Guid id,
            ProductService service,
            CancellationToken ct
        ) => service.GetAsync(id, ct));
    }
}
```

`[AsParameters]` 绑定 `page/pageSize/q` 等 URL 参数；这个入门 Service 只使用分页和 q，其余筛选尚未实现。`ProductService` 是依赖注入的服务，`CancellationToken` 由框架提供，`id` 从路径读取。

### 7.4 Program.cs：组装完整的只读应用

将学习项目 `Program.cs` 替换为：

<!-- tutorial-file: read/Program.cs -->
```csharp
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Products;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseNpgsql(DatabaseSettings.Connection(builder.Configuration)));
builder.Services.AddScoped<ProductService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", async (StoreDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "healthy" })
        : Results.StatusCode(503));
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Mini Store 入门版"));
}
app.MapProducts();
app.Run();
```

`AddScoped` 表示每个请求创建一份服务实例；DbContext 默认也是 Scoped，因此业务可以共用一个事务。`UseExceptionHandler` 统一处理 Service 抛出的 ApiError，未找到返回 404，而不是把 SQL 堆栈发给浏览器。

连接规则见 `Common/DatabaseSettings.cs`：完整 `ConnectionStrings__EcommerceLab` 配置优先，没有时使用 PG 环境变量。`.env.example` 是示例，**当前启动代码不会自动读取 .env 文件**；终端需要 export，或在 Rider Run Configuration 配置环境变量。

```bash
cd "$LEARNING_PROJECT"
dotnet build
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://127.0.0.1:5284
```

另开终端检查：

```bash
curl -i http://127.0.0.1:5284/health
curl 'http://127.0.0.1:5284/api/store/products?page=1&pageSize=3'
curl -i 'http://127.0.0.1:5284/api/store/products?pageSize=0'
curl -i http://127.0.0.1:5284/api/store/products/by-id/00000000-0000-0000-0000-000000000000
curl http://127.0.0.1:5284/openapi/v1.json
```

应分别得到健康状态、商品分页 JSON、400 参数错误、404 商品不存在和 OpenAPI JSON。复制列表中一个真实 id 替换零 UUID，再查详情。OpenAPI 是机器可读契约。打开 `http://127.0.0.1:5284/swagger`，可以在 Swagger UI 展开商品接口，点击 Try it out → Execute；页面使用同一个 `/openapi/v1.json`。

## 8. 用一条 SQL 理解刚才的 LINQ

在连接 `ecommerce_lab` 的 SQL 控制台执行：

```sql
SELECT p.public_id, p.name, p.slug, b.name AS brand_name,
       p.base_price, p.currency
FROM catalog.products p
JOIN catalog.brands b ON b.id = p.brand_id
WHERE p.status = 'active' AND p.deleted_at IS NULL
ORDER BY p.created_at DESC, p.id DESC
LIMIT 3 OFFSET 0;
```

数据库内部用 bigint 关联，API 返回 UUID。第二页、每页三条对应 `OFFSET 3`。请求超过最后一页得到空数组，不是数据库故障。

在 SQL 前加 `EXPLAIN (ANALYZE, BUFFERS)` 可以观察实际计划；ANALYZE 会真的执行这条查询，所以先从只读 SELECT 练习。更多示例见 [09_sample_queries.sql](db/09_sample_queries.sql)。

## 9. 扩展为完整电商应用

停止学习服务。现在知道请求如何经过各层，可以导入完整实现，逐功能研究。本节只操作 `LEARNING_PROJECT`，不会覆盖参考项目。

```bash
cd "$LEARNING_PROJECT"
python3 - <<'PY'
import os
import shutil
from pathlib import Path

reference = Path(os.environ['REFERENCE_PROJECT']).resolve()
learning = Path(os.environ['LEARNING_PROJECT']).resolve()
assert reference != learning, '不能把参考项目当作学习目录'
for name in ['Features', 'Data', 'Common', 'scripts', 'db', 'seed', 'docs']:
    shutil.copytree(reference / name, learning / name, dirs_exist_ok=True,
                    ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))
for name in ['Program.cs', 'appsettings.json', 'appsettings.Development.json',
             'README.md', '手摸手从零搭建ASP.NETCore电商项目.md',
             'mini-store.http', '.env.example']:
    shutil.copy2(reference / name, learning / name)
print('完整源码已导入；入门版三个 Product 文件和 Program 已替换。')
PY
dotnet build
```

前面三个入门文件被同名完整文件替换，不保留重复的类。完整版本的商品 DTO 增加含税价、库存、评分、图片及版本等字段，现有商城和管理端消费的是这套契约。

建议按下面顺序学习，而不是一次读完所有文件：

| 模块 | 从哪个方法开始 | 完成后理解什么 |
|---|---|---|
| [Auth](Features/Auth/README.md) | RegisterAsync、AddStoreAuth | 哈希、JWT、角色与每次请求的账号状态 |
| [Products](Features/Products/README.md) | CreateAsync、UpdateAsync | 商品/规格一起保存、版本冲突 |
| [Customers](Features/Customers/README.md) | SaveAddress | 本人地址、行锁、默认项唯一性 |
| [Carts](Features/Carts/README.md) | SetItemAsync | PUT 设置数量；加购不预占 |
| [Inventory](Features/Inventory/README.md) | AdjustAsync | 条件 UPDATE、防止覆盖并发库存 |
| [Checkout](Features/Checkout/README.md) | QuoteAsync、PlaceAsync | 服务端计价、报价、事务、幂等 |
| [Orders](Features/Orders/README.md) | CancelAsync、Transition | 快照与状态历史、调用方校验 |
| [Payments](Features/Payments/README.md) | RequestRefundAsync | pending 退款也占用额度 |
| [Shipping](Features/Shipping/README.md) | ShipAsync | 与预占仓匹配、原子出库 |
| [Marketing](Features/Marketing/README.md) | Fill、UpdateAsync | 已核销规则不能随意改 |
| [Reviews](Features/Reviews/README.md) | CreateAsync、ModerateAsync | 真实购买、签收时间、审核 |
| [Dashboard](Features/Dashboard/README.md) | GetAsync | 聚合、GMV/AOV 和时间窗口 |

所有完整服务都在 Program 中 AddScoped，所有路由都有对应 Map 方法。缺注册会导致服务无法解析；只注册服务、不 Map 路由，则 URL 仍然是 404。

## 10. 配置本地身份，启动完整服务

```bash
cd "$LEARNING_PROJECT"
python3 scripts/create_admin.py
ASPNETCORE_URLS=http://127.0.0.1:5284 python3 scripts/run_api.py
```

创建管理员脚本会交互要求邮箱、角色和至少 12 位密码，调用应用的 `--create-admin` 命令，不启动 HTTP 服务。使用自己的学习邮箱，不猜测项目是否有 admin/admin 这种默认凭据；已有同名账号不会被覆盖。

`run_api.py` 默认使用 Development，生成本机签名密钥到 Git 忽略的 `.local/signing-key`；完整 Program 要求密钥至少 32 字节。当前 appsettings.Development.json 开启模拟支付，其他环境不能靠修改请求参数绕开检查。

如果不使用脚本，也可用 .NET Secret Manager 保存开发配置，再用 dotnet run；开发 Secret Manager **不加密**，只帮助避免把密钥提交到项目仓库，不能替代生产密钥管理。[官方说明](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)

此教程的学习服务使用 5284。原服务可能仍在 5274，两者连接同一个实验库；这里创建的账号和学习订单会在两边看到。完整读写试验优先使用后面的隔离集成测试，不要随意修改历史 seed 来“配合例子”。

## 11. 登录、获取令牌并理解权限

使用 Rider 的 `.http` 文件或前端登录页操作。可以把参考项目的 [mini-store.http](mini-store.http) 复制到学习目录，并把 base URL 改为 5284。

注册/登录请求体使用 email、password 等字段；真实密码由你在本机工具里填写，不保存到公开请求文件。客户登录入口为 `/api/auth/login`，后台登录入口为 `/api/admin/auth/login`。

成功响应包含 `accessToken`，后续受保护请求添加：

```http
GET http://127.0.0.1:5284/api/me
Authorization: Bearer <本次客户登录返回的 accessToken>
```

尖括号内容是需要替换的说明，不是有效令牌。令牌到期重新登录；退出前端只删除当前客户端持有的令牌，服务端没有刷新令牌/会话撤销表。

| 情况 | 预期 |
|---|---|
| 不带令牌访问后台订单 | 401 |
| 客户令牌访问后台订单 | 403 |
| viewer 读取后台订单 | 200 |
| viewer 写商品 | 403 |
| operator 合法写商品 | 201 / 204 等对应成功状态 |
| 客户查询别人订单 | 404，避免泄露记录是否存在 |

这些行为有可运行的集成测试，不需要靠页面是否隐藏按钮判断安全性。

### 在 Swagger UI 中带令牌调试

打开 `http://127.0.0.1:5284/swagger`。先展开客户或后台登录接口，点击 **Try it out**，输入本机账号后 Execute；复制响应的 `accessToken`。点击页面 **Authorize**，只粘贴令牌本体，不手工加 `Bearer `。再执行对应权限的接口。切换客户与管理员时更新令牌；这里不会将令牌配置为跨刷新持久保存。

完整项目在 `Program.cs` 调用 `AddStoreOpenApi()` 注册文档服务，在 Development 分支调用 `MapOpenApi()` 和 `UseSwaggerUI()`。这个注册方法内部使用 ASP.NET Core 自带 `AddOpenApi`；只使用 Swashbuckle 的 UI 组件，无需再注册第二套 SwaggerGen。

接口文档直接跟随源码：

- `WithName` 给接口稳定且唯一的 operationId。
- `WithSummary` 是列表中的中文标题，`WithDescription` 说明身份、规则与副作用。
- `TypedResults` 或 `Produces<T>` 描述成功响应，`ProducesProblem` 描述可能的错误；这些声明不会替你执行业务校验。
- 请求 record 的 XML `param` 注释生成字段说明，需开启 csproj 的 `GenerateDocumentationFile`。
- JWT 安全声明由真实权限元数据生成；`Idempotency-Key` 因为由 HttpContext 手动读取，在下单端点显式补入文档。

元数据用法参见 [ASP.NET Core 官方文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0)。入门阶段使用少量接口；第 9 节导入完整源码后，Swagger 会展示完整分组和上述说明。

## 12. 完成一条电商业务链

完整接口请求结构参考 [API 契约](docs/backend/api-contract.md)、[OpenAPI 快照](docs/backend/openapi.json) 和 [test_api.py](scripts/test_api.py)。下面是实际调用顺序，每一步的 UUID 来自上一步响应或相应列表，不用数据库内部自增 ID 代替。

1. 客户注册或登录，保存 token。
2. `POST /api/me/addresses` 新增自己的 shipping 地址，保存地址 UUID。
3. 从商品详情选一个活动规格，`PUT /api/me/cart/items/{variantId}` 设置数量。
4. `POST /api/me/checkout/quote` 提交 addressId、shippingMethod 和可选 couponCode，保存 quoteToken。
5. `POST /api/me/orders` 提交同样的地址/配送/优惠与 quoteToken，并带新的 `Idempotency-Key`；网络重试复用同一键和请求体。
6. 开发环境调用订单的 simulate-payment；先尝试失败，再成功，观察支付记录和订单状态。
7. 后台 process 配货，用订单详情的 fulfillmentWarehouseId 发货，再 deliver 签收。
8. 客户通过订单项 UUID 提交评价，后台审核 published；商城公开评价才会出现。
9. 后台对已收款的 paymentId 申请退款并审核；观察退款与原成交金额分别保存。

这里正常创建的订单当前状态是 confirmed；历史记录同时保留 pending → confirmed。不要为了让界面看起来成功，直接 UPDATE status 跳过库存和流水操作。

### 金额为什么必须由后端计算

假设两件商品税前单价各为 1000，优惠 200，税率 10%，标准运费 500：

```text
subtotal       = 1000 × 2 = 2000
discount_total = 200
tax_total      = (2000 − 200) × 10% = 180
grand_total    = 2000 − 200 + 180 + 500 = 2480 JPY
```

真实多商品折扣由 `Pricing.Calculate` 按累计比例分摊到每行，避免末行出现负数。订单项保存整行折扣和税额，别把它们再乘一次 Quantity。

### 一个事务到底保护什么

`CheckoutService.PlaceAsync` 在同一事务里创建订单、商品/地址快照、库存预占、优惠核销、购物车转换和幂等记录。`SaveChangesAsync` 可能在事务中执行多次，只有 Commit 才提交整笔业务。

支付、退款、发货也有自己的事务。`OrderService.Transition` 本身不校验状态合法性、不保存；它是被业务方法调用的辅助函数，不应作为一个“随便传目标状态”的公开 API。

## 13. 连接商城与管理后台

完成第 9 节以后才接现有前端；入门 DTO 不满足页面所需字段。

在两个前端各自的本机 `.env.local` 配置：

```dotenv
VITE_API_BASE_URL=http://127.0.0.1:5284
```

重启各自 Vite 服务，保持原页面端口 5173 / 5174。完整后端的 CORS 已允许它们的 localhost 和 127.0.0.1 来源；如果页面换了端口，需要同步配置允许来源，不能用关闭认证来解决 CORS。

检查浏览器网络面板：请求应发往 5284，成功响应不是 mock。后台和商城查看同一订单时，金额、状态、物流应一致。前端路径和 API 路径是两层路由，不要把 `/account/orders` 当成后端的接口地址。

## 14. 格式化、编译、运行真实测试

```bash
cd "$LEARNING_PROJECT"
dotnet csharpier format .
dotnet csharpier check .
dotnet build
python3 -m venv .venv
.venv/bin/python -m pip install -r seed/requirements.txt
.venv/bin/python scripts/test_api.py
```

测试会创建随机名字 `ecommerce_lab_api_test_*` 的独立数据库，启动临时 API，执行鉴权、下单并发重放、库存、发货、退款及 SQL 审计，最后清理自己的库。角色需要 CREATEDB 权限；它不会 reset `ecommerce_lab`，也不依赖原库有多少商品。

当前检查基线为 72 项 HTTP 检查；将来增加测试，数字可能变化。重要的是命令最终 PASS，且覆盖实际数据库行为。编译成功不代表所有 LINQ 都能翻译成 SQL，单用户成功也不代表并发退款不超额。

如测试失败先读最后一个断言和 `.local/api-test.log`，不要为了通过测试删掉约束、权限或库存校验。

## 15. 新手常见故障排查

| 现象 | 先检查 |
|---|---|
| SDK 版本找不到 | dotnet --list-sdks 与 global.json 是否匹配；Rider 是否使用相同 SDK |
| PostgreSQL 连接失败 | 服务是否启动，PGHOST/PGPORT/PGUSER 是否正确；同样参数先用 psql 连接 |
| 表不存在 | 是否连到 ecommerce_lab，是否完成基础初始化与受保护的 API 增量升级 |
| `Auth:SigningKey` 缺失 | 使用 run_api.py，或正确配置开发密钥；不要放固定源码默认密钥 |
| 5284 端口占用 | 停止自己上一次服务，或换端口并同步请求 URL |
| `/swagger` 返回 404 | 是否以 Development 启动，Program 是否同时 MapOpenApi 和 UseSwaggerUI；检查请求端口 |
| 查询返回空数组 | 检查 active、软删除、筛选与页码，不等于数据库一定没有数据 |
| C# 编译通过但接口 500 | 查看服务器日志；重点检查 EF 无法翻译的 LINQ 和缺失数据库结构 |
| 商品编辑返回 409 | 重新查询当前 Version，再处理修改；不要静默覆盖别人的更新 |
| 报价 409 / 优惠券 422 | 重取当前报价，核对地址、价格、库存、有效期、门槛及次数 |
| 新注册客户后台登录失败 | 客户和后台账户独立，使用对应登录入口 |
| Rider 显示 DTO 从未实例化 | 框架会从 JSON 创建请求 record，源码已有局部注释和抑制说明，不是运行错误 |
| 已改文件但接口行为仍旧 | 没使用热重载时停止旧进程再启动；确认请求指向 5284 而不是原 5274 |

## 16. 怎样继续学，而不是只复制一遍

从小改动开始，每次只碰一条真实调用链：

1. 给商品列表添加一个已有字段，依次改 DTO、投影、前端显示。
2. 为列表增加筛选，先在 SQL 写通，再写 LINQ，并看执行计划。
3. 用两个后台窗口重现 Version 冲突，理解乐观并发。
4. 在隔离测试里重放同一幂等键，观察为什么只有一个订单。
5. 比较“未付款取消”和“已收款退款”：状态、库存、资金分别发生什么。

需要加新规则时，先写清输入、允许状态、数据库变化、失败如何回滚，再改 Service 和测试。页面能显示、接口返回 200、数据库数据正确，是三件需要分别验证的事。

这份项目可以作为本地学习和实验基础；真实上线还需真实支付回调、部署密钥、运维与生产安全设计，不要把模拟支付成功视为真实收款。
