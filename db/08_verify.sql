-- 任一异常都会 RAISE，seed 事务回滚。亦可单独用 psql -v ON_ERROR_STOP=1 -f 运行。
CREATE TEMP TABLE IF NOT EXISTS lab_checks(check_name text, violations bigint);
TRUNCATE lab_checks;
INSERT INTO lab_checks
SELECT 'order_item_totals',count(*) FROM sales.orders o LEFT JOIN (
 SELECT order_id,sum(quantity*unit_price) subtotal,sum(discount_amount) discount,sum(tax_amount) tax,sum(line_total) lines
 FROM sales.order_items GROUP BY order_id
) i ON i.order_id=o.id WHERE i.order_id IS NULL OR (o.subtotal,o.discount_total,o.tax_total,o.grand_total-o.shipping_total) IS DISTINCT FROM (i.subtotal,i.discount,i.tax,i.lines)
UNION ALL SELECT 'order_formula',count(*) FROM sales.orders WHERE grand_total<>subtotal-discount_total+tax_total+shipping_total
UNION ALL SELECT 'negative_order_money',count(*) FROM sales.orders WHERE least(subtotal,discount_total,tax_total,shipping_total,grand_total)<0
UNION ALL SELECT 'negative_item_money',count(*) FROM sales.order_items WHERE least(unit_price,discount_amount,tax_amount,line_total)<0 OR quantity<=0
UNION ALL SELECT 'negative_stock',count(*) FROM inventory.stocks WHERE quantity_on_hand<0 OR quantity_reserved<0 OR quantity_reserved>quantity_on_hand
UNION ALL SELECT 'order_before_customer',count(*) FROM sales.orders o JOIN account.customers c ON c.id=o.customer_id WHERE o.placed_at<c.created_at
UNION ALL SELECT 'order_before_product',count(*) FROM sales.order_items i JOIN sales.orders o ON o.id=i.order_id JOIN catalog.products p ON p.id=i.product_id WHERE p.published_at IS NULL OR p.published_at>o.placed_at
UNION ALL SELECT 'payment_order_amount',count(*) FROM payment.payments p JOIN sales.orders o ON o.id=p.order_id WHERE p.amount<>o.grand_total OR p.currency<>o.currency OR p.created_at<o.placed_at
UNION ALL SELECT 'paid_order_capture',count(*) FROM sales.orders o LEFT JOIN (
 SELECT order_id,sum(amount) FILTER(WHERE captured_at IS NOT NULL) captured FROM payment.payments GROUP BY order_id
) p ON p.order_id=o.id WHERE o.paid_at IS NOT NULL AND coalesce(p.captured,0)<>o.grand_total
UNION ALL SELECT 'refund_overpayment',count(*) FROM payment.payments p JOIN (
 SELECT payment_id,sum(amount) amount FROM payment.refunds WHERE status IN ('pending','completed') GROUP BY payment_id
) r ON r.payment_id=p.id WHERE r.amount>p.amount OR p.captured_at IS NULL
UNION ALL SELECT 'refund_timeline',count(*) FROM payment.refunds r JOIN payment.payments p ON p.id=r.payment_id WHERE r.created_at<p.captured_at OR r.completed_at<r.created_at OR r.amount<=0
UNION ALL SELECT 'shipment_timeline',count(*) FROM shipping.shipments s JOIN sales.orders o ON o.id=s.order_id WHERE o.paid_at IS NULL OR s.created_at<o.paid_at OR s.shipped_at<o.paid_at OR s.delivered_at<s.shipped_at OR o.status NOT IN ('paid','processing','shipped','delivered','returned')
UNION ALL SELECT 'review_purchase',count(*) FROM review.product_reviews r LEFT JOIN sales.order_items i ON i.id=r.order_item_id LEFT JOIN sales.orders o ON o.id=i.order_id WHERE r.is_verified_purchase AND (i.id IS NULL OR r.customer_id<>o.customer_id OR r.product_id<>i.product_id OR NOT EXISTS(SELECT 1 FROM shipping.shipments s WHERE s.order_id=o.id AND s.delivered_at<=r.created_at))
UNION ALL SELECT 'duplicate_email',count(*) FROM (SELECT lower(email) FROM account.customers GROUP BY lower(email) HAVING count(*)>1) d
UNION ALL SELECT 'duplicate_sku',count(*) FROM (SELECT sku FROM catalog.product_variants GROUP BY sku HAVING count(*)>1) d
UNION ALL SELECT 'duplicate_order_number',count(*) FROM (SELECT order_number FROM sales.orders GROUP BY order_number HAVING count(*)>1) d
UNION ALL SELECT 'coupon_order_total',count(*) FROM sales.orders o LEFT JOIN marketing.coupon_redemptions r ON r.order_id=o.id WHERE o.discount_total<>coalesce(r.discount_amount,0) OR (r.id IS NOT NULL AND o.customer_id<>r.customer_id)
UNION ALL SELECT 'coupon_used_count',count(*) FROM marketing.coupons c WHERE c.used_count<>(SELECT count(*) FROM marketing.coupon_redemptions r WHERE r.coupon_id=c.id)
UNION ALL SELECT 'coupon_rules',count(*) FROM marketing.coupon_redemptions r JOIN marketing.coupons c ON c.id=r.coupon_id JOIN sales.orders o ON o.id=r.order_id WHERE r.redeemed_at NOT BETWEEN c.starts_at AND c.ends_at OR o.subtotal<c.min_order_amount OR r.discount_amount<>least(o.subtotal,coalesce(c.max_discount_amount,o.subtotal),CASE WHEN c.discount_type='fixed' THEN c.discount_value ELSE floor(o.subtotal*c.discount_value/100) END)
UNION ALL SELECT 'stock_ledger_balance',count(*) FROM inventory.stocks s LEFT JOIN (
 SELECT warehouse_id,variant_id,sum(quantity) FILTER(WHERE movement_type NOT IN ('reservation','release')) hand,
 sum(quantity) FILTER(WHERE movement_type IN ('reservation','release')) reserved FROM inventory.stock_movements GROUP BY warehouse_id,variant_id
) m USING(warehouse_id,variant_id) WHERE s.quantity_on_hand<>coalesce(m.hand,0) OR s.quantity_reserved<>coalesce(m.reserved,0)
UNION ALL SELECT 'history_chain',count(*) FROM (
 SELECT order_id,from_status,to_status,lag(to_status) OVER(PARTITION BY order_id ORDER BY created_at,id) previous FROM sales.order_status_history
) h WHERE from_status IS DISTINCT FROM previous
UNION ALL SELECT 'history_final_status',count(*) FROM sales.orders o LEFT JOIN (
 SELECT DISTINCT ON(order_id) order_id,to_status FROM sales.order_status_history ORDER BY order_id,created_at DESC,id DESC
) h ON h.order_id=o.id WHERE h.to_status IS DISTINCT FROM o.status
UNION ALL SELECT 'history_before_order',count(*) FROM sales.order_status_history h JOIN sales.orders o ON o.id=h.order_id WHERE h.created_at<o.placed_at
UNION ALL SELECT 'movement_variant_not_ordered',count(*) FROM inventory.stock_movements m WHERE m.order_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sales.order_items i WHERE i.order_id=m.order_id AND i.variant_id=m.variant_id)
UNION ALL SELECT 'sale_quantities',count(*) FROM (
 SELECT i.order_id,i.variant_id,sum(i.quantity) q FROM sales.order_items i JOIN sales.orders o ON o.id=i.order_id WHERE o.status IN ('shipped','delivered','returned') GROUP BY i.order_id,i.variant_id
) i LEFT JOIN (SELECT order_id,variant_id,-sum(quantity) q FROM inventory.stock_movements WHERE movement_type='sale' GROUP BY order_id,variant_id) m USING(order_id,variant_id) WHERE i.q IS DISTINCT FROM m.q
UNION ALL SELECT 'return_quantities',count(*) FROM (
 SELECT i.order_id,i.variant_id,sum(i.quantity) q FROM sales.order_items i JOIN sales.orders o ON o.id=i.order_id WHERE o.status='returned' GROUP BY i.order_id,i.variant_id
) i LEFT JOIN (SELECT order_id,variant_id,sum(quantity) q FROM inventory.stock_movements WHERE movement_type='return' GROUP BY order_id,variant_id) m USING(order_id,variant_id) WHERE i.q IS DISTINCT FROM m.q;
-- 验证历史任意时刻，不能仅验证最后余额。窗口函数实现 running balance。
INSERT INTO lab_checks
SELECT 'stock_running_balance',count(*) FROM (
 SELECT sum(CASE WHEN movement_type IN ('reservation','release') THEN 0 ELSE quantity END) OVER w hand,
 sum(CASE WHEN movement_type IN ('reservation','release') THEN quantity ELSE 0 END) OVER w reserved
 FROM inventory.stock_movements WINDOW w AS(PARTITION BY warehouse_id,variant_id ORDER BY created_at,id ROWS UNBOUNDED PRECEDING)
) x WHERE hand<0 OR reserved<0 OR reserved>hand;
-- 所有 NUMERIC 列均为非负金额/折扣值，按目录检查，覆盖未来新增金额列。
DO $$ DECLARE r record; bad bigint; BEGIN
 FOR r IN SELECT table_schema,table_name,column_name FROM information_schema.columns
 WHERE data_type='numeric' AND table_schema IN ('account','catalog','inventory','sales','payment','shipping','marketing','review') LOOP
  EXECUTE format('SELECT count(*) FROM %I.%I WHERE %I<0',r.table_schema,r.table_name,r.column_name) INTO bad;
  INSERT INTO lab_checks VALUES('negative:'||r.table_schema||'.'||r.table_name||'.'||r.column_name,bad);
 END LOOP;
END $$;
-- 按系统目录检查每条 FK 的孤儿行，包括复合 FK；不硬编码容易漏掉的表清单。
DO $$
DECLARE fk record; joins text; nonnull text; bad bigint;
BEGIN
 FOR fk IN SELECT c.* FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE c.contype='f'
 AND n.nspname IN ('account','catalog','inventory','sales','payment','shipping','marketing','review') LOOP
  SELECT string_agg(format('ch.%I=pa.%I',a.attname,b.attname),' AND '),string_agg(format('ch.%I IS NOT NULL',a.attname),' AND ')
  INTO joins,nonnull FROM unnest(fk.conkey,fk.confkey) AS k(a,b)
  JOIN pg_attribute a ON a.attrelid=fk.conrelid AND a.attnum=k.a JOIN pg_attribute b ON b.attrelid=fk.confrelid AND b.attnum=k.b;
  EXECUTE format('SELECT count(*) FROM %s ch WHERE %s AND NOT EXISTS(SELECT 1 FROM %s pa WHERE %s)',fk.conrelid::regclass,nonnull,fk.confrelid::regclass,joins) INTO bad;
  INSERT INTO lab_checks VALUES('fk:'||fk.conname,bad);
 END LOOP;
END $$;
SELECT * FROM lab_checks ORDER BY check_name;
DO $$ DECLARE failures text; BEGIN
 SELECT string_agg(check_name||'='||violations,', ') INTO failures FROM lab_checks WHERE violations<>0;
 IF failures IS NOT NULL THEN RAISE EXCEPTION 'Verification failed: %',failures; END IF;
 RAISE NOTICE 'All verification checks passed';
END $$;
-- 所有业务表的精确行数（不用 pg_stat 的估算）。
CREATE TEMP TABLE IF NOT EXISTS lab_row_counts(table_name text,row_count bigint);
TRUNCATE lab_row_counts;
DO $$ DECLARE r record; n bigint; BEGIN
 FOR r IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('account','catalog','inventory','sales','payment','shipping','marketing','review') LOOP
  EXECUTE format('SELECT count(*) FROM %I.%I',r.schemaname,r.tablename) INTO n;
  INSERT INTO lab_row_counts VALUES(r.schemaname||'.'||r.tablename,n);
 END LOOP;
END $$;
SELECT * FROM lab_row_counts ORDER BY table_name;
