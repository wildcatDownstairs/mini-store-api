-- psql -X -v ON_ERROR_STOP=1 -d ecommerce_lab -f db/09_sample_queries.sql
SET TIME ZONE 'Asia/Tokyo';
-- 1 最近 30 天成交额：包含税费与运费；已付款，未减退款。改 now() 可练固定历史窗口。
SELECT sum(grand_total) AS paid_gmv FROM sales.orders WHERE paid_at IS NOT NULL AND created_at>=now()-interval '30 days';
-- 2 / 3 商品销量与税前毛销售额 TOP 20。
SELECT * FROM catalog.product_sales_summary ORDER BY total_quantity_sold DESC,product_id LIMIT 20;
SELECT * FROM catalog.product_sales_summary ORDER BY gross_sales DESC,product_id LIMIT 20;
-- 4 GROUP BY 把相同月份聚合；税费运费均包含，退款另外统计，避免混淆 GMV 与净收入。
SELECT date_trunc('month',placed_at) AS month,count(*) orders,sum(grand_total) gmv
FROM sales.orders WHERE paid_at IS NOT NULL GROUP BY 1 ORDER BY 1;
-- 5 JOIN 通过外键关联顾客与订单。
SELECT c.public_id,c.last_name,c.first_name,count(*) order_count,sum(o.grand_total) total_paid
FROM account.customers c JOIN sales.orders o ON o.customer_id=c.id
WHERE o.paid_at IS NOT NULL GROUP BY c.id ORDER BY total_paid DESC LIMIT 20;
-- 6 HAVING 在聚合之后过滤；WHERE 在聚合前过滤。
SELECT customer_id,count(*) paid_orders FROM sales.orders WHERE paid_at IS NOT NULL
GROUP BY customer_id HAVING count(*)>=2 ORDER BY paid_orders DESC LIMIT 20;
-- 7 AOV：已支付订单的平均含税、含运费订单金额。
SELECT round(avg(grand_total),2) AS aov FROM sales.orders WHERE paid_at IS NOT NULL;
-- 8 状态分布。
SELECT status,count(*) FROM sales.orders GROUP BY status ORDER BY count(*) DESC;
-- 9 补货预警按可售量判断，不能只看 on_hand。
SELECT w.code,v.sku,s.quantity_on_hand,s.quantity_reserved,s.reorder_level
FROM inventory.stocks s JOIN inventory.warehouses w ON w.id=s.warehouse_id
JOIN catalog.product_variants v ON v.id=s.variant_id
WHERE s.quantity_on_hand-s.quantity_reserved<s.reorder_level ORDER BY w.code,v.sku LIMIT 50;
-- 10 全部变体跨仓均无可售库存、但商品仍上架。
SELECT p.id,p.name FROM catalog.products p JOIN catalog.product_inventory_summary v ON v.product_id=p.id
WHERE p.status='active' GROUP BY p.id HAVING sum(v.available_stock)=0;
-- 11 优惠券额度使用率（usage_limit 分母）；另外给出订单使用优惠券的比例。
SELECT code,used_count,usage_limit,round(100.0*used_count/nullif(usage_limit,0),2) AS quota_percent FROM marketing.coupons;
SELECT round(100.0*(SELECT count(*) FROM marketing.coupon_redemptions)/nullif(count(*),0),2) AS order_coupon_percent FROM sales.orders;
-- 12 CTE 给子查询起名字；先各自聚合，避免一对多 JOIN 导致支付金额重复。
WITH captured AS (SELECT sum(amount) amount,count(*) n FROM payment.payments WHERE captured_at IS NOT NULL),
refunded AS (SELECT sum(amount) amount,count(DISTINCT payment_id) n FROM payment.refunds WHERE status='completed')
SELECT round(100.0*r.amount/nullif(p.amount,0),2) amount_refund_percent,
round(100.0*r.n/nullif(p.n,0),2) payment_refund_percent FROM captured p CROSS JOIN refunded r;
-- 13 商品可属于多个分类。每个商品归最细层分类中的最小 ID，避免多分类重复计入。
WITH primary_category AS (
 SELECT DISTINCT ON(pc.product_id) pc.product_id,pc.category_id
 FROM catalog.product_categories pc JOIN catalog.categories c ON c.id=pc.category_id
 ORDER BY pc.product_id,(c.parent_id IS NOT NULL) DESC,pc.category_id
)
SELECT c.name,sum(i.line_total-i.tax_amount) net_merchandise_sales
FROM sales.order_items i JOIN sales.orders o ON o.id=i.order_id
JOIN primary_category pc ON pc.product_id=i.product_id JOIN catalog.categories c ON c.id=pc.category_id
WHERE o.paid_at IS NOT NULL GROUP BY c.id ORDER BY net_merchandise_sales DESC;
-- 14 只统计已发布评价。
SELECT product_id,count(*) review_count,round(avg(rating),2) average_rating FROM review.product_reviews
WHERE status='published' GROUP BY product_id ORDER BY review_count DESC LIMIT 20;
-- 15 指定顾客订单历史；替换 42，应用程序必须参数化。
SELECT public_id,order_number,status,grand_total,created_at FROM sales.orders
WHERE customer_id=42 ORDER BY created_at DESC,id DESC LIMIT 20;
-- 16 OFFSET 简单但深分页会扫描并跳过越来越多的行。
SELECT public_id,name,base_price FROM catalog.products ORDER BY created_at DESC,id DESC LIMIT 20 OFFSET 100;
-- keyset 复合游标包含时间与 id，避免同一时间重复/漏行。
WITH cursor_row AS (SELECT created_at,id FROM catalog.products ORDER BY created_at DESC,id DESC OFFSET 99 LIMIT 1)
SELECT p.public_id,p.name,p.base_price FROM catalog.products p,cursor_row c
WHERE (p.created_at,p.id)<(c.created_at,c.id) ORDER BY p.created_at DESC,p.id DESC LIMIT 20;
-- 17 多表详情先独立聚合各个一对多关系，否则 items x payments x shipments 会放大行数。
SELECT o.order_number,c.last_name,c.first_name,o.grand_total,
(SELECT jsonb_agg(jsonb_build_object('sku',i.sku,'quantity',i.quantity,'total',i.line_total)) FROM sales.order_items i WHERE i.order_id=o.id) items,
(SELECT jsonb_agg(jsonb_build_object('provider',p.provider,'status',p.status,'amount',p.amount)) FROM payment.payments p WHERE p.order_id=o.id) payments,
(SELECT jsonb_agg(jsonb_build_object('carrier',s.carrier,'tracking',s.tracking_number)) FROM shipping.shipments s WHERE s.order_id=o.id) shipments
FROM sales.orders o JOIN account.customers c ON c.id=o.customer_id WHERE o.id=42;
-- 18 ROW_NUMBER：每位顾客最新三笔订单；窗口函数保留原始行，不像 GROUP BY 合并行。
WITH numbered AS (
 SELECT customer_id,order_number,placed_at,row_number() OVER(PARTITION BY customer_id ORDER BY placed_at DESC,id DESC) rn
 FROM sales.orders WHERE customer_id IN (42,43,44)
) SELECT * FROM numbered WHERE rn<=3 ORDER BY customer_id,rn;
-- 19 RANK：并列销量名次相同，后续名次会跳号。
SELECT product,total_quantity_sold,rank() OVER(ORDER BY total_quantity_sold DESC) sales_rank
FROM catalog.product_sales_summary ORDER BY sales_rank,product_id LIMIT 20;
-- 20 SUM OVER：先月汇总，再计算累计 GMV。
WITH monthly AS (
 SELECT date_trunc('month',placed_at) AS month,sum(grand_total) gmv FROM sales.orders
 WHERE paid_at IS NOT NULL GROUP BY 1
) SELECT month,gmv,sum(gmv) OVER(ORDER BY month ROWS UNBOUNDED PRECEDING) cumulative_gmv FROM monthly ORDER BY month;
-- 21 搜索：前导通配符通常不能用普通 B-tree，故意保留作练习；目前不引入搜索扩展。
SELECT public_id,name FROM catalog.products WHERE name ILIKE '%イヤホン%' LIMIT 20;

-- EXPLAIN 不运行查询；ANALYZE 会实际执行，写操作需谨慎。BUFFERS 展示缓存/磁盘页。
-- A 唯一索引点查（替换 email 可与实际样本对比）。
EXPLAIN (ANALYZE,BUFFERS) SELECT * FROM account.customers WHERE lower(email)='sakura.sato.42@mail.example';
-- B 非索引表达式 / 模糊搜索；不要用 enable_seqscan=off 强迫漂亮计划。
EXPLAIN (ANALYZE,BUFFERS) SELECT * FROM catalog.products WHERE description ILIKE '%品質%';
-- C 组合索引：第一列等值，之后直接按索引顺序读取。
EXPLAIN (ANALYZE,BUFFERS) SELECT * FROM sales.orders WHERE customer_id=42 ORDER BY created_at DESC,id DESC LIMIT 20;
-- D 深分页，比较 keyset 与 OFFSET。顾客查询与全量分页使用不同索引。
EXPLAIN (ANALYZE,BUFFERS) SELECT id,created_at FROM sales.orders ORDER BY created_at DESC,id DESC LIMIT 20 OFFSET 80000;
EXPLAIN (ANALYZE,BUFFERS) SELECT id,created_at FROM sales.orders WHERE (created_at,id)<('2026-01-01T00:00:00+09:00',50000) ORDER BY created_at DESC,id DESC LIMIT 20;
-- E FK JOIN，观察 Nested Loop / Hash Join / Bitmap Scan 的选择。
EXPLAIN (ANALYZE,BUFFERS) SELECT o.order_number,i.sku,i.quantity FROM sales.orders o JOIN sales.order_items i ON i.order_id=o.id WHERE o.customer_id=42;
