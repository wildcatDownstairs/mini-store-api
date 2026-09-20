# 数据库设计与业务口径

## 领域与表

| Schema | 表 | 职责 |
|---|---|---|
| account | customers / customer_addresses | 客户、登录标识、当前地址簿 |
| catalog | brands / categories | 品牌、支持多层父子的分类树 |
| catalog | products / product_variants | 商品展示信息、实际销售 SKU 与变体属性 |
| catalog | product_categories / product_images | 多对多分类、商品与可选变体图片 |
| inventory | warehouses / stocks / stock_movements | 仓库、当前库存、带订单关联的库存流水 |
| sales | carts / cart_items | 购物车与商品项，部分唯一索引限制每人一个 active cart |
| sales | orders / order_items | 订单头、下单名称与价格快照 |
| sales | order_addresses / order_status_history | 地址快照、按时间记录合法状态流转 |
| payment | payments / refunds | 支付尝试（含失败重试）与退款 |
| shipping | shipments | 发货仓、承运商与配送时间 |
| marketing | coupons / coupon_redemptions | 优惠券额度、订单使用记录 |
| review | product_reviews | 购买关系、非均匀评分及审核状态 |

共 23 张表，public 无业务表。每个可独立暴露的主要实体都有唯一 UUID；内部关联使用 bigint IDENTITY ALWAYS。关联表、明细、快照以内部键为主，不为每行额外创建 public UUID。

## 类型与约束

- PostgreSQL 18 实际存在 `uuidv7()` 时设为列默认，否则用核心 `gen_random_uuid()`；最低支持 PostgreSQL 13。
- API 不能因 UUID 不连续就省略鉴权。seed 的 UUID 根据固定时间和固定 PRNG 构造，18 上是 v7，旧版是 v4；新增业务记录仍由数据库默认函数生成。
- 所有事件时间使用 TIMESTAMPTZ；出生日期是 DATE；按日本当地日期分析时先 `SET TIME ZONE 'Asia/Tokyo'`。
- 金额为 NUMERIC(12,2)，货币 CHAR(3)。当前 seed 全部 JPY，按整数日元运算。未来不能跨货币直接 SUM。
- 价格是税前价；食品模拟 8%，其他类目模拟 10%，仅为实验计价规则。折扣分摊到各行并把余数放到最后一行，税按行四舍五入；运费固定为最终收费额。订单总额=各行含税净额+运费。
- 状态用 VARCHAR + CHECK，便于 EF migration。金额非负、数量正数、评分 1～5、变体与产品一致等由数据库约束保证。
- 邮箱使用 `lower(email)` 唯一索引，包括已软删除账号，防止历史身份被复用。默认地址按 customer + address_type 唯一。
- `updated_at` 由写入方显式维护，避免修改历史数据时隐式触发器把时间改成今天。EF 更新时需同步赋值。

## 删除策略

- customers→addresses/carts、carts→cart_items、products→images/product_categories 使用 CASCADE：真正的当前子资源。
- orders、order_items、payments、refunds、shipments、reviews 及其上游 customer/product/variant 一律 RESTRICT；不会因为删商品而删历史订单。
- 分类父节点 RESTRICT，删除前应重新分类；额外触发器防止树形循环。
- 本项目未选用 SET NULL，因为审计关系需保留。退市商品用 status/deleted_at，不物理删除。
- images 用 (variant_id, product_id) 复合 FK，避免把其他商品的变体图片挂进来；order_items 同样校验产品与变体一致。

## 索引如何支持查询

`db/05_indexes.sql` 每个索引有用途注释；PK/UNIQUE 自带 B-tree，不再为相同前缀重复建索引。

| 索引/前缀 | 查询模式 |
|---|---|
| customers lower(email) UNIQUE | 不区分大小写登录、查重 |
| products slug UNIQUE；variants sku UNIQUE | API slug / SKU 点查 |
| products (status, created_at DESC, id DESC) | 上架商品列表与稳定分页 |
| products (created_at DESC, id DESC) | 所有状态的商品时间排序 |
| orders (customer_id, created_at DESC, id DESC) | 客户订单历史，也覆盖单独 customer_id |
| orders (status, created_at DESC, id DESC) | 待处理订单队列 |
| orders (created_at DESC, id DESC) | 时间范围、全量分页 |
| order_items order_id / product_id / variant_id | 订单详情、商品销售聚合、SKU 历史 |
| payments order_id；transaction_id UNIQUE | 订单支付尝试、支付回调去重 |
| shipments order_id；tracking_number UNIQUE | 订单物流、物流单号查找 |
| reviews (product_id, created_at DESC, id DESC) | 商品最新评价，也覆盖 product_id |
| stocks PK (warehouse_id, variant_id)；variant_id | 单仓预占、跨仓库存汇总 |
| movements (variant_id, created_at, id)；order_id partial | SKU 流水追溯、订单库存审计 |
| carts active customer partial UNIQUE | 每位客户最多一个当前购物车 |

其他关联索引支持 FK 删除检查、子资源列表。未给每个状态字段单独建索引，低选择性状态经常更适合顺序扫描。未启用 trigram/全文检索扩展，ILIKE 前导通配符用于对比扫描成本。查询计划选择由统计信息和数据分布决定，不保证“有索引就用”。

## 种子分布与边界

固定 `SEED=20260918`、`SEED_AS_OF=2026-09-18T00:00:00+00:00`，固定依赖版本，使用独立随机源生成 UUID。相同参数与 PostgreSQL UUID 能力下，业务字段和 public_id 可重现；数据库物理页、统计采样、文件大小不保证字节相同。

最近 1095 天中，前 40 天用于客户注册与目录建档；订单日权重指数增长，周末 1.18 倍，年末/新年/Golden Week/11 月促销周/7 月促销周 3.5 倍；18～23 时段权重更高。客户与热门 SKU 使用偏斜抽样，存在高频客户、热销与长尾商品。最近订单保持未完成状态，超过 14 天的旧订单收敛为已交付、已退货或已取消，不伪造未来物流。

日文姓名、匹配的日本邮编/城市/町名、手机号形状均为合成数据。`.example` 邮箱和图片地址特意不可投递/不依赖外部网络；假 Argon2 风格哈希包含 LAB_ONLY 标记，不能用于登录。品牌和描述为合成文案。目录目前在初期建档、使用稳定价格；没有额外价格历史表。

medium 基线：20,000 客户，30,000 地址，150 品牌，90 分类，5,000 商品，12,000 变体，100,000 订单。每单 1～6 项，配送主要来自已付款订单，评论来自已收货订单，五星权重 55%、四星 27%、三星 10%、二星 5%、一星 3%。部分付款有失败重试；退货全额退款，少数已送达订单因延误部分退款。单订单 seed 只用一个仓发货，但结构支持多个 shipment。少量订单使用优惠券。

库存按真实事件时间处理：初始进货→下单预占→出库解除预占并销售扣减→退货入库，缺货时生成小批量补货。reservation/release 只影响 reserved；purchase/sale/return/adjustment 影响 on_hand。最终余额和历史任意时点的非负性均验证。采购 reference_id 可以为空；订单 reference_type 生成 order_id 并由 FK 校验。未添加采购单模块。预占函数同时限制累计预占不超过订单项数量，防止重复预占。

small / medium / large 的订单数为 5,000 / 100,000 / 500,000。large 的客户、商品、变体、订单、品牌、分类、仓库基线均为 medium 的 5 倍；派生记录数按同样概率生成，不能保证每一个随机派生表恰好乘 5。按 2,000 订单一批 COPY，但整个导入是一个事务；客户/目录/当前库存映射驻内存，未来未执行事件用时间堆保存。large 会消耗更多 RAM、磁盘与 WAL，仍然不需要外部服务。

## 事务与并发练习边界

`inventory.reserve_stock(warehouse,variant,quantity,order)` 通过订单行锁和单条条件 UPDATE 防止可售库存变负；预占与流水同事务失败回滚。支付退款触发器锁住父支付行，校验已 capture、时间、待处理+已完成退款总额，防止并发超额退款。支付金额更新也受退款总额保护。

行内金额等式、FK、唯一性、非负数、合法状态边、退款上限由数据库执行；订单头与多行明细聚合一致、优惠券额度更新、完整状态流转、出库/退货、支付与物流跨表状态应由未来 API 事务统一维护，`08_verify.sql` 是可重复运行的跨表审计，不是替代业务事务的自动同步器。实验并非完整支付网关或库存服务。外部支付不能与数据库形成一个 ACID 事务，未来可练幂等回调与 outbox，但现在不加消息队列。

订单审计记录不应随意修改；verified review 写入触发器验证客户、商品和收货时间。未来多应用写入时可对角色做最小权限限制，本任务不创建或修改已有用户权限。

GMV 指已付款订单含税、含运费成交额，含后来退货的成交记录；退款额单独报告。商品 gross_sales 为折扣前税前金额；品类查询为折扣后税前金额。多分类汇总显式选最细层分类中的最小 ID，避免双重计算。所有统计口径都在 SQL 中注释。
