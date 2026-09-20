-- PostgreSQL 13+ 内置 gen_random_uuid()；18+ 内置 uuidv7()。
-- 无需第三方扩展。public_id 默认值在 04 中按实际函数可用性设置。
DO $$ BEGIN
 IF current_setting('server_version_num')::int < 130000 THEN
  RAISE EXCEPTION 'Requires PostgreSQL 13 or newer';
 END IF;
END $$;
