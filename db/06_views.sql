CREATE VIEW sales.order_summary AS
SELECT o.id,o.public_id,o.order_number,c.last_name||' '||c.first_name AS customer,o.status,
 coalesce(i.item_count,0) AS item_count,o.subtotal,o.discount_total AS discount,o.tax_total AS tax,
 o.shipping_total AS shipping,o.grand_total,o.placed_at
FROM sales.orders o JOIN account.customers c ON c.id=o.customer_id
LEFT JOIN (SELECT order_id,sum(quantity) AS item_count FROM sales.order_items GROUP BY order_id) i ON i.order_id=o.id;
CREATE VIEW catalog.product_inventory_summary AS
SELECT p.id AS product_id,p.name AS product,v.id AS variant_id,v.name AS variant,v.sku,
 coalesce(sum(s.quantity_on_hand),0) AS total_stock,coalesce(sum(s.quantity_reserved),0) AS reserved_stock,
 coalesce(sum(s.quantity_on_hand-s.quantity_reserved),0) AS available_stock
FROM catalog.products p JOIN catalog.product_variants v ON v.product_id=p.id
LEFT JOIN inventory.stocks s ON s.variant_id=v.id GROUP BY p.id,v.id;
-- 已支付订单的商品税前毛额；包括随后退货的成交记录，不等于退款后的净收入。
CREATE VIEW catalog.product_sales_summary AS
SELECT p.id AS product_id,p.name AS product,coalesce(s.total_quantity_sold,0) AS total_quantity_sold,
 coalesce(s.gross_sales,0) AS gross_sales,coalesce(s.order_count,0) AS order_count
FROM catalog.products p LEFT JOIN (
 SELECT i.product_id,sum(i.quantity) AS total_quantity_sold,sum(i.unit_price*i.quantity) AS gross_sales,
 count(DISTINCT i.order_id) AS order_count FROM sales.order_items i JOIN sales.orders o ON o.id=i.order_id
 WHERE o.paid_at IS NOT NULL GROUP BY i.product_id
) s ON s.product_id=p.id;
