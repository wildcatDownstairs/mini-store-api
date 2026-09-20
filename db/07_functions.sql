-- 并发扣库存的起点：单条 UPDATE 原子比较可用量，流水与预占同一事务。
CREATE OR REPLACE FUNCTION inventory.reserve_stock(p_warehouse_id bigint,p_variant_id bigint,p_quantity int,p_order_id bigint)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
 IF p_quantity<=0 THEN RAISE EXCEPTION 'quantity must be positive'; END IF;
 PERFORM 1 FROM sales.orders WHERE id=p_order_id AND status IN ('confirmed','paid','processing') FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION 'order not reservable'; END IF;
 IF NOT EXISTS (SELECT 1 FROM sales.order_items WHERE order_id=p_order_id AND variant_id=p_variant_id) THEN
  RAISE EXCEPTION 'variant not in order';
 END IF;
 IF p_quantity + coalesce((SELECT sum(quantity) FROM inventory.stock_movements WHERE order_id=p_order_id AND variant_id=p_variant_id AND movement_type IN ('reservation','release')),0) > (SELECT sum(quantity) FROM sales.order_items WHERE order_id=p_order_id AND variant_id=p_variant_id) THEN
  RAISE EXCEPTION 'reservation exceeds ordered quantity';
 END IF;
 UPDATE inventory.stocks SET quantity_reserved=quantity_reserved+p_quantity,updated_at=now()
 WHERE warehouse_id=p_warehouse_id AND variant_id=p_variant_id AND quantity_on_hand-quantity_reserved>=p_quantity;
 IF NOT FOUND THEN RAISE EXCEPTION 'insufficient stock or missing stock row'; END IF;
 INSERT INTO inventory.stock_movements(warehouse_id,variant_id,movement_type,quantity,reference_type,reference_id,note,created_at)
 VALUES(p_warehouse_id,p_variant_id,'reservation',p_quantity,'order',p_order_id,'API reservation',clock_timestamp());
END $$;
-- 退款写入锁定支付行，阻止并发退款合计超额；支付金额后续修改也检查。
CREATE OR REPLACE FUNCTION payment.guard_refund() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE paid numeric; captured timestamptz; total numeric;
BEGIN
 IF TG_TABLE_NAME='refunds' THEN
  IF TG_OP='UPDATE' AND NEW.payment_id<>OLD.payment_id THEN RAISE EXCEPTION 'refund payment is immutable'; END IF;
  SELECT amount,captured_at INTO paid,captured FROM payment.payments WHERE id=NEW.payment_id FOR UPDATE;
  SELECT coalesce(sum(amount),0) INTO total FROM payment.refunds
   WHERE payment_id=NEW.payment_id AND status IN ('pending','completed') AND id<>NEW.id;
  IF captured IS NULL OR NEW.created_at<captured OR (NEW.status IN ('pending','completed') AND total+NEW.amount>paid) THEN
   RAISE EXCEPTION 'refund exceeds captured payment or invalid capture timeline';
  END IF;
 ELSE
  SELECT coalesce(sum(amount),0) INTO total FROM payment.refunds WHERE payment_id=NEW.id AND status IN ('pending','completed');
  IF total>NEW.amount OR (total>0 AND NEW.captured_at IS NULL) THEN RAISE EXCEPTION 'payment conflicts with refunds'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER refund_guard BEFORE INSERT OR UPDATE ON payment.refunds FOR EACH ROW EXECUTE FUNCTION payment.guard_refund();
CREATE TRIGGER payment_refund_guard BEFORE UPDATE ON payment.payments FOR EACH ROW EXECUTE FUNCTION payment.guard_refund();
CREATE OR REPLACE FUNCTION review.guard_purchase() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.order_item_id IS NOT NULL AND NOT EXISTS (
 SELECT 1 FROM sales.order_items i JOIN sales.orders o ON o.id=i.order_id
 JOIN shipping.shipments s ON s.order_id=o.id
 WHERE i.id=NEW.order_item_id AND i.product_id=NEW.product_id AND o.customer_id=NEW.customer_id
 AND s.delivered_at IS NOT NULL AND s.delivered_at<=NEW.created_at
 ) THEN RAISE EXCEPTION 'review does not match a delivered purchase'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER review_purchase_guard BEFORE INSERT OR UPDATE ON review.product_reviews FOR EACH ROW EXECUTE FUNCTION review.guard_purchase();
-- 防止多级循环分类；目录更新按事务 advisory lock 串行化。
CREATE OR REPLACE FUNCTION catalog.guard_category_tree() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(20260918,2);
 IF NEW.parent_id IS NOT NULL AND EXISTS (
 WITH RECURSIVE ancestors AS (
 SELECT id,parent_id FROM catalog.categories WHERE id=NEW.parent_id
 UNION SELECT c.id,c.parent_id FROM catalog.categories c JOIN ancestors a ON c.id=a.parent_id
 ) SELECT 1 FROM ancestors WHERE id=NEW.id
 ) THEN RAISE EXCEPTION 'category cycle'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER category_tree_guard BEFORE INSERT OR UPDATE OF parent_id ON catalog.categories FOR EACH ROW EXECUTE FUNCTION catalog.guard_category_tree();
