-- UNIQUE/PK 已自带索引，slug、sku、order_number、provider_transaction_id、tracking_number 不重复创建。
CREATE UNIQUE INDEX customers_email_lower_uq ON account.customers(lower(email)); -- 登录 / email 查重
CREATE UNIQUE INDEX address_default_uq ON account.customer_addresses(customer_id,address_type) WHERE is_default;
CREATE INDEX customer_addresses_customer_idx ON account.customer_addresses(customer_id);
CREATE INDEX categories_parent_idx ON catalog.categories(parent_id);
CREATE INDEX products_status_created_idx ON catalog.products(status,created_at DESC,id DESC); -- 状态过滤并分页
CREATE INDEX products_created_idx ON catalog.products(created_at DESC,id DESC); -- 全量 keyset 分页
CREATE INDEX products_brand_idx ON catalog.products(brand_id);
CREATE INDEX variants_product_idx ON catalog.product_variants(product_id);
CREATE INDEX product_categories_category_idx ON catalog.product_categories(category_id,product_id);
CREATE INDEX product_images_product_idx ON catalog.product_images(product_id,sort_order);
CREATE UNIQUE INDEX product_images_primary_uq ON catalog.product_images(product_id) WHERE is_primary;
CREATE INDEX stocks_variant_idx ON inventory.stocks(variant_id); -- 跨仓汇总，PK 已覆盖仓库+SKU
CREATE INDEX stock_movements_variant_time_idx ON inventory.stock_movements(variant_id,created_at,id);
CREATE INDEX stock_movements_order_idx ON inventory.stock_movements(order_id) WHERE order_id IS NOT NULL;
CREATE UNIQUE INDEX carts_active_customer_uq ON sales.carts(customer_id) WHERE status='active';
CREATE INDEX carts_customer_idx ON sales.carts(customer_id);
CREATE INDEX cart_items_variant_idx ON sales.cart_items(variant_id);
CREATE INDEX orders_customer_created_idx ON sales.orders(customer_id,created_at DESC,id DESC);
CREATE INDEX orders_status_created_idx ON sales.orders(status,created_at DESC,id DESC);
CREATE INDEX orders_created_idx ON sales.orders(created_at DESC,id DESC);
CREATE INDEX order_items_order_idx ON sales.order_items(order_id);
CREATE INDEX order_items_product_idx ON sales.order_items(product_id);
CREATE INDEX order_items_variant_idx ON sales.order_items(variant_id);
CREATE INDEX order_history_order_time_idx ON sales.order_status_history(order_id,created_at,id);
CREATE INDEX payments_order_idx ON payment.payments(order_id);
CREATE INDEX refunds_payment_idx ON payment.refunds(payment_id);
CREATE INDEX shipments_order_idx ON shipping.shipments(order_id);
CREATE INDEX shipments_warehouse_idx ON shipping.shipments(warehouse_id);
CREATE INDEX redemptions_coupon_idx ON marketing.coupon_redemptions(coupon_id);
CREATE INDEX redemptions_customer_idx ON marketing.coupon_redemptions(customer_id);
CREATE INDEX reviews_product_created_idx ON review.product_reviews(product_id,created_at DESC,id DESC);
CREATE INDEX reviews_customer_idx ON review.product_reviews(customer_id);
