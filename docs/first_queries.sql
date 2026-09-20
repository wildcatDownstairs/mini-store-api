-- 建议在 psql 中逐条执行，并尝试修改筛选条件。
-- 1. 查看最近订单：WHERE / ORDER BY / LIMIT。
SELECT order_number,status,grand_total,created_at
FROM sales.orders ORDER BY created_at DESC,id DESC LIMIT 10;

-- 2. JOIN：将订单与顾客关联。
SELECT o.order_number,c.last_name,c.first_name,o.grand_total
FROM sales.orders o JOIN account.customers c ON c.id=o.customer_id
WHERE c.id=42 ORDER BY o.created_at DESC,o.id DESC LIMIT 20;

-- 3. GROUP BY：统计每种状态的数量和金额。
SELECT status,count(*) AS orders,sum(grand_total) AS amount
FROM sales.orders GROUP BY status ORDER BY orders DESC;

-- 4. 库存：区分实物库存与已预占库存。
SELECT sku,total_stock,reserved_stock,available_stock
FROM catalog.product_inventory_summary
ORDER BY available_stock,variant_id LIMIT 20;

-- 5. 执行计划：观察客户+时间组合索引。
EXPLAIN (ANALYZE,BUFFERS)
SELECT order_number,grand_total,created_at FROM sales.orders
WHERE customer_id=42 ORDER BY created_at DESC,id DESC LIMIT 20;
