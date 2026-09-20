DO $$
DECLARE r record; uuid_default text;
BEGIN
 uuid_default := CASE WHEN to_regprocedure('pg_catalog.uuidv7()') IS NOT NULL THEN 'pg_catalog.uuidv7()' ELSE 'pg_catalog.gen_random_uuid()' END;
 FOR r IN SELECT table_schema,table_name FROM information_schema.columns WHERE column_name='public_id'
 AND table_schema IN ('account','catalog','inventory','sales','payment','shipping','marketing','review') LOOP
  EXECUTE format('ALTER TABLE %I.%I ALTER COLUMN public_id SET DEFAULT %s',r.table_schema,r.table_name,uuid_default);
  EXECUTE format('ALTER TABLE %I.%I ADD UNIQUE(public_id)',r.table_schema,r.table_name);
 END LOOP;
END $$;
ALTER TABLE inventory.stock_movements ADD COLUMN order_id BIGINT
 GENERATED ALWAYS AS (CASE WHEN reference_type='order' THEN reference_id END) STORED
 REFERENCES sales.orders ON DELETE RESTRICT;
ALTER TABLE sales.order_status_history ADD CONSTRAINT valid_transition CHECK (COALESCE(
 (from_status IS NULL AND to_status='pending') OR
 (from_status='pending' AND to_status IN ('confirmed','cancelled')) OR
 (from_status='confirmed' AND to_status IN ('paid','cancelled')) OR
 (from_status='paid' AND to_status IN ('processing','cancelled')) OR
 (from_status='processing' AND to_status IN ('shipped','cancelled')) OR
 (from_status='shipped' AND to_status='delivered') OR
 (from_status='delivered' AND to_status='returned')
, false));

ALTER TABLE sales.orders ADD CONSTRAINT order_state_timestamps CHECK (
 (status NOT IN ('paid','processing','shipped','delivered','returned') OR paid_at IS NOT NULL)
 AND (status NOT IN ('pending','confirmed') OR paid_at IS NULL)
 AND (status IN ('delivered','returned')) = (completed_at IS NOT NULL)
 AND (status='cancelled') = (cancelled_at IS NOT NULL)
);
ALTER TABLE payment.payments ADD CONSTRAINT capture_requires_authorization CHECK(captured_at IS NULL OR authorized_at IS NOT NULL);
