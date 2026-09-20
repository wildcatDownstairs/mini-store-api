-- 简体中文数据库注释；可重复执行，只更新元数据，不修改业务记录。
-- 由 manage_db.py comments 在事务中执行；初始化时也会自动执行。
SET LOCAL lock_timeout = '5s';


COMMENT ON TABLE account.customer_addresses IS '客户当前地址簿：支持收货地址、账单地址及各类型默认地址；修改不会影响历史订单地址快照。';
COMMENT ON COLUMN account.customer_addresses.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN account.customer_addresses.customer_id IS '所属客户的内部主键，关联 account.customers.id。';
COMMENT ON COLUMN account.customer_addresses.address_type IS '地址类型：shipping 表示收货地址，billing 表示账单地址。';
COMMENT ON COLUMN account.customer_addresses.recipient_name IS '收件人或账单接收人姓名。';
COMMENT ON COLUMN account.customer_addresses.postal_code IS '邮政编码；日本地址通常采用三位数字加连字符加四位数字。';
COMMENT ON COLUMN account.customer_addresses.country_code IS '两位国家或地区代码，例如 JP 表示日本。';
COMMENT ON COLUMN account.customer_addresses.prefecture IS '都道府县名称，例如东京都或大阪府。';
COMMENT ON COLUMN account.customer_addresses.city IS '市、区、町或村名称。';
COMMENT ON COLUMN account.customer_addresses.address_line1 IS '详细地址第一行，通常包含町名、丁目及门牌号。';
COMMENT ON COLUMN account.customer_addresses.address_line2 IS '详细地址第二行，通常包含楼名和房间号，可为空。';
COMMENT ON COLUMN account.customer_addresses.phone IS '联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。';
COMMENT ON COLUMN account.customer_addresses.is_default IS '是否为该客户在此地址类型下的默认地址；同一客户和地址类型最多一条默认地址。';
COMMENT ON COLUMN account.customer_addresses.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN account.customer_addresses.updated_at IS '记录最后更新时间，由写入方显式维护。';

COMMENT ON TABLE account.customers IS '客户账户：保存客户身份、联系信息和账户状态；删除优先使用软删除。';
COMMENT ON COLUMN account.customers.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN account.customers.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN account.customers.email IS '客户邮箱；通过 lower(email) 唯一索引保证大小写不敏感的唯一性，软删除后仍保留唯一约束。';
COMMENT ON COLUMN account.customers.password_hash IS '密码哈希；实验数据为不可用于登录的假 Argon2 风格字符串，禁止保存明文密码。';
COMMENT ON COLUMN account.customers.first_name IS '客户名字，不含姓氏。';
COMMENT ON COLUMN account.customers.last_name IS '客户姓氏。';
COMMENT ON COLUMN account.customers.phone IS '联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。';
COMMENT ON COLUMN account.customers.birth_date IS '出生日期，仅保存日期，不含时分秒。';
COMMENT ON COLUMN account.customers.status IS '账户状态：active 正常、disabled 停用、pending 待激活。';
COMMENT ON COLUMN account.customers.email_verified_at IS '邮箱验证通过时间；为空表示尚未验证。';
COMMENT ON COLUMN account.customers.last_login_at IS '最近一次登录时间；为空表示没有登录记录。';
COMMENT ON COLUMN account.customers.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN account.customers.updated_at IS '记录最后更新时间，由写入方显式维护。';
COMMENT ON COLUMN account.customers.deleted_at IS '软删除时间；为空表示未软删除。';

COMMENT ON TABLE catalog.brands IS '商品品牌：保存品牌名称、访问标识和所属国家。';
COMMENT ON COLUMN catalog.brands.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN catalog.brands.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN catalog.brands.name IS '品牌显示名称。';
COMMENT ON COLUMN catalog.brands.slug IS '用于页面地址或 API 查询的可读唯一标识。';
COMMENT ON COLUMN catalog.brands.country_code IS '品牌所属国家或地区的两位代码，例如 JP。';
COMMENT ON COLUMN catalog.brands.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON TABLE catalog.categories IS '树形商品分类：通过父分类关联支持多层目录。';
COMMENT ON COLUMN catalog.categories.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN catalog.categories.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN catalog.categories.parent_id IS '父分类内部主键，关联 catalog.categories.id；为空表示根分类，不允许形成循环。';
COMMENT ON COLUMN catalog.categories.name IS '分类显示名称。';
COMMENT ON COLUMN catalog.categories.slug IS '用于页面地址或 API 查询的可读唯一标识。';
COMMENT ON COLUMN catalog.categories.sort_order IS '显示排序值，通常按升序展示。';
COMMENT ON COLUMN catalog.categories.is_active IS '分类是否启用，控制目录展示。';
COMMENT ON COLUMN catalog.categories.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON TABLE catalog.product_categories IS '商品与分类的多对多关联；同一商品和分类组合不可重复。';
COMMENT ON COLUMN catalog.product_categories.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN catalog.product_categories.category_id IS '分类内部主键，关联 catalog.categories.id。';

COMMENT ON TABLE catalog.product_images IS '商品图片：可关联具体变体；不关联变体时作为商品通用图片。';
COMMENT ON COLUMN catalog.product_images.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN catalog.product_images.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN catalog.product_images.variant_id IS '可选的变体内部主键；为空表示商品通用图片，非空时必须属于同一商品。';
COMMENT ON COLUMN catalog.product_images.url IS '图片资源地址；种子数据使用示例地址。';
COMMENT ON COLUMN catalog.product_images.alt_text IS '图片替代文字，供图片无法加载或辅助阅读时使用。';
COMMENT ON COLUMN catalog.product_images.sort_order IS '显示排序值，通常按升序展示。';
COMMENT ON COLUMN catalog.product_images.is_primary IS '是否为商品主图。';
COMMENT ON COLUMN catalog.product_images.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON VIEW catalog.product_inventory_summary IS '商品变体库存汇总视图：跨仓库汇总实物库存、预占库存及可售库存。';
COMMENT ON COLUMN catalog.product_inventory_summary.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN catalog.product_inventory_summary.product IS '当前商品名称。';
COMMENT ON COLUMN catalog.product_inventory_summary.variant_id IS '商品变体内部主键，关联 catalog.product_variants.id。';
COMMENT ON COLUMN catalog.product_inventory_summary.variant IS '当前商品变体名称。';
COMMENT ON COLUMN catalog.product_inventory_summary.sku IS '库存管理编码，唯一标识一个商品变体。';
COMMENT ON COLUMN catalog.product_inventory_summary.total_stock IS '该变体跨仓库的实物库存总量。';
COMMENT ON COLUMN catalog.product_inventory_summary.reserved_stock IS '该变体跨仓库的预占库存总量。';
COMMENT ON COLUMN catalog.product_inventory_summary.available_stock IS '该变体跨仓库的可售库存，等于实物库存总量减预占库存总量。';

COMMENT ON VIEW catalog.product_sales_summary IS '商品销量汇总视图：按已付款订单统计，包含后续退货记录，不扣减退款；跨货币使用时需调整分组口径。';
COMMENT ON COLUMN catalog.product_sales_summary.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN catalog.product_sales_summary.product IS '当前商品名称。';
COMMENT ON COLUMN catalog.product_sales_summary.total_quantity_sold IS '已付款订单的商品总销售件数，包括后续退货订单，不扣除退货件数。';
COMMENT ON COLUMN catalog.product_sales_summary.gross_sales IS '已付款订单的折扣前税前商品销售额，等于成交单价乘数量之和；不减退款，不含税费和运费。';
COMMENT ON COLUMN catalog.product_sales_summary.order_count IS '包含该商品的已付款订单去重数量，包括后续退货订单。';

COMMENT ON TABLE catalog.product_variants IS '商品变体：每行代表一个可售规格及其唯一 SKU，保存规格售价与属性。';
COMMENT ON COLUMN catalog.product_variants.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN catalog.product_variants.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN catalog.product_variants.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN catalog.product_variants.sku IS '库存管理编码，唯一标识一个商品变体。';
COMMENT ON COLUMN catalog.product_variants.barcode IS '商品条码；非空时必须唯一。';
COMMENT ON COLUMN catalog.product_variants.name IS '变体规格显示名称，例如黑色／512GB。';
COMMENT ON COLUMN catalog.product_variants.price IS '商品变体税前售价，非负，币种由 currency 指定。';
COMMENT ON COLUMN catalog.product_variants.currency IS '三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。';
COMMENT ON COLUMN catalog.product_variants.attributes IS '变体规格属性的 JSON 对象，例如颜色、尺码或容量；核心关联仍使用外键。';
COMMENT ON COLUMN catalog.product_variants.weight_grams IS '商品变体重量，单位为克，必须非负。';
COMMENT ON COLUMN catalog.product_variants.is_active IS '该变体是否启用；可售性还需结合商品状态和可售库存判断。';
COMMENT ON COLUMN catalog.product_variants.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN catalog.product_variants.updated_at IS '记录最后更新时间，由写入方显式维护。';

COMMENT ON TABLE catalog.products IS '商品展示主体：保存品牌、名称、介绍及上下架状态；实际售卖规格见商品变体。';
COMMENT ON COLUMN catalog.products.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN catalog.products.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN catalog.products.brand_id IS '品牌内部主键，关联 catalog.brands.id。';
COMMENT ON COLUMN catalog.products.name IS '商品当前显示名称。';
COMMENT ON COLUMN catalog.products.slug IS '用于页面地址或 API 查询的可读唯一标识。';
COMMENT ON COLUMN catalog.products.description IS '商品详细介绍，可为空。';
COMMENT ON COLUMN catalog.products.status IS '商品状态：draft 草稿、active 上架、inactive 下架、archived 已归档。';
COMMENT ON COLUMN catalog.products.base_price IS '商品税前基础展示价格；实际下单单价来自所选变体。';
COMMENT ON COLUMN catalog.products.currency IS '三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。';
COMMENT ON COLUMN catalog.products.published_at IS '商品发布时间；为空表示尚未发布。';
COMMENT ON COLUMN catalog.products.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN catalog.products.updated_at IS '记录最后更新时间，由写入方显式维护。';
COMMENT ON COLUMN catalog.products.deleted_at IS '软删除时间；为空表示未软删除。';

COMMENT ON TABLE inventory.stock_movements IS '库存变动流水：进货、销售、退货、调整影响实物库存，预占和释放影响预占库存。';
COMMENT ON COLUMN inventory.stock_movements.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN inventory.stock_movements.warehouse_id IS '仓库内部主键，关联 inventory.warehouses.id。';
COMMENT ON COLUMN inventory.stock_movements.variant_id IS '商品变体内部主键，关联 catalog.product_variants.id。';
COMMENT ON COLUMN inventory.stock_movements.movement_type IS '变动类型：purchase 进货、sale 销售出库、return 退货入库、adjustment 库存调整、reservation 预占、release 释放预占。';
COMMENT ON COLUMN inventory.stock_movements.quantity IS '本次变动数量，不得为零；进货、退货、预占为正，销售、释放为负，调整可正可负；预占和释放仅改变预占库存。';
COMMENT ON COLUMN inventory.stock_movements.reference_type IS '来源类型：order 订单、purchase 采购、adjustment 库存调整。';
COMMENT ON COLUMN inventory.stock_movements.reference_id IS '来源记录标识；来源为订单时必须填写订单内部主键；采购及调整尚未建立对应业务表。';
COMMENT ON COLUMN inventory.stock_movements.note IS '补充说明或操作备注。';
COMMENT ON COLUMN inventory.stock_movements.created_at IS '库存变动发生时间，按此时间及流水主键重建库存余额。';
COMMENT ON COLUMN inventory.stock_movements.order_id IS '生成列：来源类型为 order 时取 reference_id，否则为空；外键校验来源订单存在，不可手动写入。';

COMMENT ON TABLE inventory.stocks IS '仓库与商品变体的当前库存；可售库存等于实物库存减预占库存。';
COMMENT ON COLUMN inventory.stocks.warehouse_id IS '仓库内部主键，关联 inventory.warehouses.id。';
COMMENT ON COLUMN inventory.stocks.variant_id IS '商品变体内部主键，关联 catalog.product_variants.id。';
COMMENT ON COLUMN inventory.stocks.quantity_on_hand IS '仓库实际持有数量，包含已预占部分，必须非负。';
COMMENT ON COLUMN inventory.stocks.quantity_reserved IS '已预占但尚未出库的数量，介于零和实物库存之间。';
COMMENT ON COLUMN inventory.stocks.reorder_level IS '补货预警阈值，单位为件，必须非负。';
COMMENT ON COLUMN inventory.stocks.updated_at IS '记录最后更新时间，由写入方显式维护。';

COMMENT ON TABLE inventory.warehouses IS '仓库资料：保存仓库编码、名称及日本仓库地址。';
COMMENT ON COLUMN inventory.warehouses.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN inventory.warehouses.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN inventory.warehouses.code IS '唯一仓库编码，供库存与发货业务引用。';
COMMENT ON COLUMN inventory.warehouses.name IS '仓库显示名称。';
COMMENT ON COLUMN inventory.warehouses.postal_code IS '邮政编码；日本地址通常采用三位数字加连字符加四位数字。';
COMMENT ON COLUMN inventory.warehouses.prefecture IS '都道府县名称，例如东京都或大阪府。';
COMMENT ON COLUMN inventory.warehouses.city IS '市、区、町或村名称。';
COMMENT ON COLUMN inventory.warehouses.address IS '仓库详细地址。';
COMMENT ON COLUMN inventory.warehouses.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON TABLE marketing.coupon_redemptions IS '优惠券核销记录：关联客户和订单，保存实际优惠金额；当前每笔订单最多使用一张优惠券。';
COMMENT ON COLUMN marketing.coupon_redemptions.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN marketing.coupon_redemptions.coupon_id IS '优惠券内部主键，关联 marketing.coupons.id。';
COMMENT ON COLUMN marketing.coupon_redemptions.customer_id IS '所属客户的内部主键，关联 account.customers.id。';
COMMENT ON COLUMN marketing.coupon_redemptions.order_id IS '订单内部主键，关联 sales.orders.id。';
COMMENT ON COLUMN marketing.coupon_redemptions.discount_amount IS '本次订单实际使用优惠券减免的税前商品金额，应等于该订单 discount_total。';
COMMENT ON COLUMN marketing.coupon_redemptions.redeemed_at IS '优惠券实际核销时间。';

COMMENT ON TABLE marketing.coupons IS '优惠券规则：定义折扣、门槛、有效期和使用次数限制。';
COMMENT ON COLUMN marketing.coupons.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN marketing.coupons.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN marketing.coupons.code IS '唯一优惠码，供客户结算时输入；当前唯一约束区分大小写。';
COMMENT ON COLUMN marketing.coupons.name IS '优惠券活动显示名称。';
COMMENT ON COLUMN marketing.coupons.discount_type IS '优惠类型：percentage 按百分比减免，fixed 固定金额减免。';
COMMENT ON COLUMN marketing.coupons.discount_value IS '优惠数值；percentage 时为减免百分比（10 表示减免 10%，即九折），fixed 时为固定减免金额。';
COMMENT ON COLUMN marketing.coupons.min_order_amount IS '使用优惠券所需的折扣前税前商品最低金额，不含运费。';
COMMENT ON COLUMN marketing.coupons.max_discount_amount IS '单次优惠金额上限；为空表示不单独限制，但优惠不得超过商品小计。';
COMMENT ON COLUMN marketing.coupons.usage_limit IS '优惠券最多可核销次数；为空表示不限次数。';
COMMENT ON COLUMN marketing.coupons.used_count IS '已核销次数，应与核销记录数一致，由业务事务维护。';
COMMENT ON COLUMN marketing.coupons.starts_at IS '优惠券有效期开始时间。';
COMMENT ON COLUMN marketing.coupons.ends_at IS '优惠券有效期结束时间，必须晚于开始时间。';
COMMENT ON COLUMN marketing.coupons.is_active IS '优惠券是否启用；核销仍需校验有效期、金额门槛及使用次数。';
COMMENT ON COLUMN marketing.coupons.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON TABLE payment.payments IS '订单支付尝试：允许失败重试产生多条记录，记录支付渠道、金额及授权和扣款时间。';
COMMENT ON COLUMN payment.payments.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN payment.payments.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN payment.payments.order_id IS '订单内部主键，关联 sales.orders.id。';
COMMENT ON COLUMN payment.payments.provider IS '支付服务商：stripe、paypal、paypay 或 card_gateway（银行卡支付网关）。';
COMMENT ON COLUMN payment.payments.provider_transaction_id IS '支付服务商交易编号；非空时全表唯一，用于对账及回调去重。';
COMMENT ON COLUMN payment.payments.method IS '支付方式：credit_card 信用卡、paypal 贝宝、paypay 电子支付、bank_transfer 银行转账。';
COMMENT ON COLUMN payment.payments.status IS '支付状态：pending 待处理、authorized 已授权、captured 已扣款、failed 失败、cancelled 已取消、refunded 已全额退款、partially_refunded 已部分退款。';
COMMENT ON COLUMN payment.payments.amount IS '本次支付尝试的金额；当前业务采用整单支付，应与订单应付总额及币种一致；退款后保留原金额。';
COMMENT ON COLUMN payment.payments.currency IS '三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。';
COMMENT ON COLUMN payment.payments.authorized_at IS '支付授权成功时间；扣款前必须已有授权时间。';
COMMENT ON COLUMN payment.payments.captured_at IS '支付实际扣款成功时间；退款及部分退款后仍保留。';
COMMENT ON COLUMN payment.payments.failed_at IS '支付失败时间。';
COMMENT ON COLUMN payment.payments.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON TABLE payment.refunds IS '支付退款记录：待处理与已完成退款合计不得超过对应已扣款支付金额。';
COMMENT ON COLUMN payment.refunds.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN payment.refunds.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN payment.refunds.payment_id IS '支付记录内部主键，关联 payment.payments.id。';
COMMENT ON COLUMN payment.refunds.amount IS '本次退款金额，必须大于零；使用原支付币种，待处理与已完成退款合计不能超过已扣款金额。';
COMMENT ON COLUMN payment.refunds.reason IS '退款原因，不能为空值。';
COMMENT ON COLUMN payment.refunds.status IS '退款状态：pending 待处理、completed 已完成、failed 失败。';
COMMENT ON COLUMN payment.refunds.provider_refund_id IS '支付服务商退款编号；非空时全表唯一，用于退款对账。';
COMMENT ON COLUMN payment.refunds.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN payment.refunds.completed_at IS '退款完成时间；仅 completed 状态非空。';

COMMENT ON TABLE review.product_reviews IS '商品评价：保存评分、文字与审核状态；已验证购买评价须关联真实购买明细及收货时间。';
COMMENT ON COLUMN review.product_reviews.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN review.product_reviews.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN review.product_reviews.customer_id IS '所属客户的内部主键，关联 account.customers.id。';
COMMENT ON COLUMN review.product_reviews.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN review.product_reviews.order_item_id IS '购买明细内部主键，关联 sales.order_items.id；非空时唯一，限制同一订单项重复评价。';
COMMENT ON COLUMN review.product_reviews.rating IS '商品评分，取值为一至五星。';
COMMENT ON COLUMN review.product_reviews.title IS '评价标题，可为空。';
COMMENT ON COLUMN review.product_reviews.content IS '评价文字内容，可为空，允许仅提交评分。';
COMMENT ON COLUMN review.product_reviews.is_verified_purchase IS '是否为已验证购买评价；为真时须关联同客户同商品的已收货订单项，由触发器校验。';
COMMENT ON COLUMN review.product_reviews.status IS '评价审核状态：pending 待审核、published 已发布、rejected 已拒绝。';
COMMENT ON COLUMN review.product_reviews.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN review.product_reviews.updated_at IS '记录最后更新时间，由写入方显式维护。';

COMMENT ON TABLE sales.cart_items IS '购物车商品项：记录变体、数量及当时单价；结算时应重新校验价格与库存。';
COMMENT ON COLUMN sales.cart_items.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN sales.cart_items.cart_id IS '购物车内部主键，关联 sales.carts.id。';
COMMENT ON COLUMN sales.cart_items.variant_id IS '商品变体内部主键，关联 catalog.product_variants.id。';
COMMENT ON COLUMN sales.cart_items.quantity IS '购买数量，必须为正整数。';
COMMENT ON COLUMN sales.cart_items.unit_price IS '加入购物车时记录的变体税前单价；结算时需重新确认，不代表已锁定成交价格。';
COMMENT ON COLUMN sales.cart_items.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN sales.cart_items.updated_at IS '记录最后更新时间，由写入方显式维护。';

COMMENT ON TABLE sales.carts IS '客户购物车：每位客户最多一个使用中的购物车。';
COMMENT ON COLUMN sales.carts.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN sales.carts.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN sales.carts.customer_id IS '所属客户的内部主键，关联 account.customers.id。';
COMMENT ON COLUMN sales.carts.status IS '购物车状态：active 使用中、converted 已转订单、abandoned 已放弃。';
COMMENT ON COLUMN sales.carts.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN sales.carts.updated_at IS '记录最后更新时间，由写入方显式维护。';
COMMENT ON COLUMN sales.carts.checked_out_at IS '购物车转为订单的时间；仅 converted 状态非空。';

COMMENT ON TABLE sales.order_addresses IS '订单地址快照：保存下单时收货及账单地址，不依赖客户当前地址簿。';
COMMENT ON COLUMN sales.order_addresses.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN sales.order_addresses.order_id IS '订单内部主键，关联 sales.orders.id。';
COMMENT ON COLUMN sales.order_addresses.address_type IS '下单时的地址快照：地址类型：shipping 表示收货地址，billing 表示账单地址。';
COMMENT ON COLUMN sales.order_addresses.recipient_name IS '下单时的地址快照：收件人或账单接收人姓名。';
COMMENT ON COLUMN sales.order_addresses.postal_code IS '下单时的地址快照：邮政编码；日本地址通常采用三位数字加连字符加四位数字。';
COMMENT ON COLUMN sales.order_addresses.country_code IS '下单时的地址快照：两位国家或地区代码，例如 JP 表示日本。';
COMMENT ON COLUMN sales.order_addresses.prefecture IS '下单时的地址快照：都道府县名称，例如东京都或大阪府。';
COMMENT ON COLUMN sales.order_addresses.city IS '下单时的地址快照：市、区、町或村名称。';
COMMENT ON COLUMN sales.order_addresses.address_line1 IS '下单时的地址快照：详细地址第一行，通常包含町名、丁目及门牌号。';
COMMENT ON COLUMN sales.order_addresses.address_line2 IS '下单时的地址快照：详细地址第二行，通常包含楼名和房间号，可为空。';
COMMENT ON COLUMN sales.order_addresses.phone IS '下单时的地址快照：联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。';

COMMENT ON TABLE sales.order_items IS '订单商品快照：保留下单时的 SKU、名称、成交价格及折扣税额，后续商品修改不影响历史订单。';
COMMENT ON COLUMN sales.order_items.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN sales.order_items.order_id IS '订单内部主键，关联 sales.orders.id。';
COMMENT ON COLUMN sales.order_items.product_id IS '商品内部主键，关联 catalog.products.id。';
COMMENT ON COLUMN sales.order_items.variant_id IS '商品变体内部主键，关联 catalog.product_variants.id。';
COMMENT ON COLUMN sales.order_items.sku IS '下单时的商品变体 SKU 快照，不随当前 SKU 修改而更新。';
COMMENT ON COLUMN sales.order_items.product_name IS '下单时的商品名称快照，不随商品改名而更新。';
COMMENT ON COLUMN sales.order_items.variant_name IS '下单时的变体名称快照，不随规格改名而更新。';
COMMENT ON COLUMN sales.order_items.quantity IS '购买数量，必须为正整数。';
COMMENT ON COLUMN sales.order_items.unit_price IS '下单成交时的税前单价快照，后续调价不影响该值。';
COMMENT ON COLUMN sales.order_items.discount_amount IS '分摊至此订单项的整行折扣金额，不是单件折扣，不能超过数量乘单价。';
COMMENT ON COLUMN sales.order_items.tax_amount IS '此订单项在折扣分摊后的消费税额；为整行税额。';
COMMENT ON COLUMN sales.order_items.line_total IS '订单项含税净额，等于数量乘税前单价减整行折扣加整行税额，不含运费。';
COMMENT ON COLUMN sales.order_items.created_at IS '记录创建时间，使用带时区时间戳。';

COMMENT ON TABLE sales.order_status_history IS '订单状态流转记录：按时间保存前后状态及变更原因，供追踪和审计。';
COMMENT ON COLUMN sales.order_status_history.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN sales.order_status_history.order_id IS '订单内部主键，关联 sales.orders.id。';
COMMENT ON COLUMN sales.order_status_history.from_status IS '变更前订单状态；首条记录为空，表示订单刚创建。';
COMMENT ON COLUMN sales.order_status_history.to_status IS '变更后订单状态：pending 待确认、confirmed 已确认、paid 已付款、processing 处理中、shipped 已发货、delivered 已送达、cancelled 已取消、returned 已退货；仅允许约束定义的状态流转。';
COMMENT ON COLUMN sales.order_status_history.reason IS '订单状态变更原因，可为空。';
COMMENT ON COLUMN sales.order_status_history.created_at IS '本次订单状态变更发生时间。';

COMMENT ON VIEW sales.order_summary IS '订单摘要视图：聚合客户姓名、商品总件数、金额与下单时间。';
COMMENT ON COLUMN sales.order_summary.id IS '订单内部主键，来自 sales.orders.id。';
COMMENT ON COLUMN sales.order_summary.public_id IS '订单对外 UUID 标识，来自 sales.orders.public_id。';
COMMENT ON COLUMN sales.order_summary.order_number IS '唯一的可读订单编号，供客户查询和业务对账使用。';
COMMENT ON COLUMN sales.order_summary.customer IS '订单客户姓名，由姓氏和名字拼接。';
COMMENT ON COLUMN sales.order_summary.status IS '订单当前状态；含义与 sales.orders.status 相同。';
COMMENT ON COLUMN sales.order_summary.item_count IS '订单内商品总件数，等于明细 quantity 之和，不是明细行数。';
COMMENT ON COLUMN sales.order_summary.subtotal IS '折扣前税前商品金额，等于所有订单项数量乘单价之和。';
COMMENT ON COLUMN sales.order_summary.discount IS '订单优惠总额，对应 sales.orders.discount_total。';
COMMENT ON COLUMN sales.order_summary.tax IS '订单商品税额总计，对应 sales.orders.tax_total。';
COMMENT ON COLUMN sales.order_summary.shipping IS '订单配送费用，对应 sales.orders.shipping_total。';
COMMENT ON COLUMN sales.order_summary.grand_total IS '订单应付总额，等于商品小计减优惠总额加税额加配送费。';
COMMENT ON COLUMN sales.order_summary.placed_at IS '客户下单时间。';

COMMENT ON TABLE sales.orders IS '订单主表：保存成交金额、订单状态与关键时间；与明细、支付和物流共同组成订单业务。';
COMMENT ON COLUMN sales.orders.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN sales.orders.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN sales.orders.order_number IS '唯一的可读订单编号，供客户查询和业务对账使用。';
COMMENT ON COLUMN sales.orders.customer_id IS '所属客户的内部主键，关联 account.customers.id。';
COMMENT ON COLUMN sales.orders.status IS '订单状态：pending 待确认、confirmed 已确认、paid 已付款、processing 处理中、shipped 已发货、delivered 已送达、cancelled 已取消、returned 已退货。';
COMMENT ON COLUMN sales.orders.currency IS '三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。';
COMMENT ON COLUMN sales.orders.subtotal IS '折扣前税前商品金额，等于所有订单项数量乘单价之和。';
COMMENT ON COLUMN sales.orders.discount_total IS '订单优惠总额，等于订单项分摊折扣之和，与优惠券实际核销金额对应。';
COMMENT ON COLUMN sales.orders.tax_total IS '折后商品消费税总额，等于订单项税额之和。';
COMMENT ON COLUMN sales.orders.shipping_total IS '最终收取的配送费，不参与商品折扣计算。';
COMMENT ON COLUMN sales.orders.grand_total IS '订单应付总额，等于商品小计减优惠总额加税额加配送费。';
COMMENT ON COLUMN sales.orders.placed_at IS '客户下单时间。';
COMMENT ON COLUMN sales.orders.paid_at IS '订单付款成功时间；为空表示尚未记录付款成功。';
COMMENT ON COLUMN sales.orders.cancelled_at IS '订单取消时间；仅 cancelled 状态非空。';
COMMENT ON COLUMN sales.orders.completed_at IS '订单送达完成时间；delivered 和 returned 状态必须有值，退货后保留原完成时间。';
COMMENT ON COLUMN sales.orders.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN sales.orders.updated_at IS '记录最后更新时间，由写入方显式维护。';

COMMENT ON TABLE shipping.shipments IS '订单物流记录：保存发货仓、承运商、运单号与配送时间；订单可拆成多个包裹。';
COMMENT ON COLUMN shipping.shipments.id IS '内部主键，使用 bigint 自增标识列，供表间关联使用。';
COMMENT ON COLUMN shipping.shipments.public_id IS '对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。';
COMMENT ON COLUMN shipping.shipments.order_id IS '订单内部主键，关联 sales.orders.id。';
COMMENT ON COLUMN shipping.shipments.warehouse_id IS '仓库内部主键，关联 inventory.warehouses.id。';
COMMENT ON COLUMN shipping.shipments.carrier IS '承运商名称，例如 Yamato、Sagawa 或 Japan Post。';
COMMENT ON COLUMN shipping.shipments.tracking_number IS '物流运单号；非空时全表唯一。';
COMMENT ON COLUMN shipping.shipments.status IS '物流状态：pending 待处理、ready 待发货、shipped 已发货、in_transit 运输中、delivered 已送达、failed 配送失败、returned 已退回。';
COMMENT ON COLUMN shipping.shipments.shipped_at IS '实际发货时间。';
COMMENT ON COLUMN shipping.shipments.delivered_at IS '签收或送达时间，不得早于发货时间。';
COMMENT ON COLUMN shipping.shipments.created_at IS '记录创建时间，使用带时区时间戳。';
COMMENT ON COLUMN shipping.shipments.updated_at IS '记录最后更新时间，由写入方显式维护。';

-- 覆盖检查：新增表或字段后必须同步补充注释。
DO $$
BEGIN
 IF EXISTS (
  SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
  WHERE n.nspname IN ('account','catalog','inventory','sales','payment','shipping','marketing','review')
    AND c.relkind IN ('r','p','v')
    AND (coalesce(obj_description(c.oid,'pg_class'),'') !~ '[一-龥]'
      OR coalesce(col_description(c.oid,a.attnum),'') !~ '[一-龥]')
 ) THEN
  RAISE EXCEPTION '存在缺少中文注释的业务表、视图或字段，请补充 db/10_comments.sql';
 END IF;
END $$;
