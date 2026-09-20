-- 由 scripts/manage_db.py 在确认不存在后执行，不可放在 transaction 中。
CREATE DATABASE ecommerce_lab TEMPLATE template0 ENCODING 'UTF8';
COMMENT ON DATABASE ecommerce_lab IS 'mini_store:ecommerce_lab:v1:managed-learning-dataset';
