# 后端与前后台联调验证

验证日期：2026-09-20，日本时间。以下为实际执行结果，不是计划。

## 自动验证

- C#：CSharpier 检查 50 个文件通过；`dotnet build` 零警告、零错误。
- 后端：`scripts/test_api.py` 在独立随机数据库通过 60 个 HTTP 检查，并通过 SQL 全量一致性审计。覆盖管理员/客户隔离、只读权限、资源归属、商品版本冲突、默认地址、报价/下单、同键并发重放、支付失败/成功/重复调用、取消、发货签收、评论审核、并发超额退款保护和各后台查询。
- 原库：`db/08_verify.sql` 全部规则异常数为 0；随后执行 ANALYZE。验证包括外键、金额、优惠券核销、退款总额、库存/预占与流水余额、时间线和状态历史。
- 中文注释：增量表、字段及原有表/视图共 269 个字段，269 个具有说明；幂等 comments 命令执行通过。
- 商城：5 个展示映射/交互测试通过，正式构建通过。
- 后台：请求适配测试、TypeScript 类型检查与正式构建通过；本次修改的业务页面/请求层/路由通过现有 ESLint 检查。

## 浏览器交叉验证

在本机浏览器真实完成注册、登录、地址保存、商品规格选择、加购、报价、下单、模拟付款，然后切换后台处理同一笔订单，配货、发货、签收，再回商城评价、后台审核发布，最后确认商品详情中可见评价。

- 订单：`MS-20260920-ae3d9c42b9b44f0a9e243c6`。
- 商品：Nami Works 調光デスクライト 結-065，Natural，数量 1。
- 税前 ¥4,500 + 税 ¥450 + 运费 ¥500 = ¥5,450；商城、后台和数据库一致。
- 状态：pending → confirmed → paid → processing → shipped → delivered。
- 商品可售量从 142 变成 141。库存流水：[('reservation', 1), ('release', -1), ('sale', -1)]。
- 物流：Yamato，教学单号 QA20260920130301；商城显示相同的发货和签收时间。
- 评价：提交后待审核，后台发布后商品页公开显示，重复评价按钮被禁用。
- 临时联调客户与临时管理员已停用；订单/流水/评价保留用于观察。可用学习管理员与只读账号仅保存在本机 `.local/learning-accounts.json`，不进入 GitHub。也可自己运行 `scripts/create_admin.py` 创建新账号。

## 联调中修复的问题

修复了 EF 部分唯一索引误推断一对一导航、xmin 原始 SQL 映射、可选查询参数绑定、带时区优惠券日期、预占流水时间早于下单时间、缺失的前端 DTO 字段、账户资料入口与旧 Mock 测试。开发服务器修改路由模块时出现的模块加载失败经重新加载后恢复；实际验证页面正常加载和业务流转。格式化后再次构建及检查，修复了一个 TypeScript 请求体类型推断问题。

## 数据库规模

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
