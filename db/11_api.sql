-- 后端增量升级：重复运行安全，不删除现有业务数据。
SET LOCAL lock_timeout = '5s';
ALTER TABLE account.customer_addresses ADD COLUMN IF NOT EXISTS public_id uuid NOT NULL DEFAULT gen_random_uuid();
ALTER TABLE sales.order_items ADD COLUMN IF NOT EXISTS public_id uuid NOT NULL DEFAULT gen_random_uuid();
CREATE UNIQUE INDEX IF NOT EXISTS addresses_public_id_uq ON account.customer_addresses(public_id);
CREATE UNIQUE INDEX IF NOT EXISTS order_items_public_id_uq ON sales.order_items(public_id);
DO $$ BEGIN
 IF to_regprocedure('pg_catalog.uuidv7()') IS NOT NULL THEN
  ALTER TABLE account.customer_addresses ALTER COLUMN public_id SET DEFAULT uuidv7();
  ALTER TABLE sales.order_items ALTER COLUMN public_id SET DEFAULT uuidv7();
 END IF;
END $$;
COMMENT ON COLUMN account.customer_addresses.public_id IS '地址对外 UUID 标识；用于 API 定位，仍须校验客户归属。';
COMMENT ON COLUMN sales.order_items.public_id IS '订单项对外 UUID 标识；用于提交购买评价，仍须校验订单归属。';
CREATE TABLE IF NOT EXISTS account.admin_users (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 public_id uuid NOT NULL DEFAULT gen_random_uuid() UNIQUE,
 email varchar(254) NOT NULL,
 password_hash text NOT NULL,
 display_name varchar(80) NOT NULL,
 role varchar(16) NOT NULL CHECK(role IN ('operator','viewer')),
 is_active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS admin_email_lower_uq ON account.admin_users(lower(email));
COMMENT ON TABLE account.admin_users IS '后台人员账户：与商城客户身份分离；运营人员可写，只读人员只能查询。';
COMMENT ON COLUMN account.admin_users.id IS '管理员内部自增主键。';
COMMENT ON COLUMN account.admin_users.public_id IS '管理员对外 UUID 标识。';
COMMENT ON COLUMN account.admin_users.email IS '登录邮箱，不区分大小写唯一。';
COMMENT ON COLUMN account.admin_users.password_hash IS 'ASP.NET Core PasswordHasher 生成的加盐密码哈希，不保存明文。';
COMMENT ON COLUMN account.admin_users.display_name IS '后台人员显示名称。';
COMMENT ON COLUMN account.admin_users.role IS '角色：operator 运营人员；viewer 只读人员。';
COMMENT ON COLUMN account.admin_users.is_active IS '是否允许登录及访问后台。';
COMMENT ON COLUMN account.admin_users.created_at IS '管理员账户创建时间。';
CREATE TABLE IF NOT EXISTS sales.checkout_requests (
 customer_id bigint NOT NULL REFERENCES account.customers(id) ON DELETE RESTRICT,
 request_key varchar(128) NOT NULL,
 request_hash varchar(64) NOT NULL,
 order_id bigint NOT NULL REFERENCES sales.orders(id) ON DELETE RESTRICT,
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(customer_id,request_key)
);
COMMENT ON TABLE sales.checkout_requests IS '已完成下单请求的幂等记录：相同客户和键返回原订单；同键不同请求拒绝。与订单在同一事务提交。';
COMMENT ON COLUMN sales.checkout_requests.customer_id IS '发起下单的客户内部主键。';
COMMENT ON COLUMN sales.checkout_requests.request_key IS '客户端 Idempotency-Key，长度最多 128，客户端重试需复用。';
COMMENT ON COLUMN sales.checkout_requests.request_hash IS '请求内容的 SHA-256 摘要，避免同一幂等键被用于不同请求。';
COMMENT ON COLUMN sales.checkout_requests.order_id IS '成功创建的订单内部主键。';
COMMENT ON COLUMN sales.checkout_requests.created_at IS '幂等请求成功提交时间。';

DO $$ BEGIN IF to_regprocedure('pg_catalog.uuidv7()') IS NOT NULL THEN ALTER TABLE account.admin_users ALTER COLUMN public_id SET DEFAULT uuidv7(); END IF; END $$;

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
