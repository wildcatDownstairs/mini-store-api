-- 内部 ID 用 bigint；所有 public_id 在 04 中添加版本兼容的默认值及 UNIQUE。
CREATE TABLE account.customers (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 email VARCHAR(254) NOT NULL CHECK (email = btrim(email) AND position('@' in email)>1),
 password_hash TEXT NOT NULL, first_name VARCHAR(80) NOT NULL, last_name VARCHAR(80) NOT NULL,
 phone VARCHAR(24), birth_date DATE, status VARCHAR(16) NOT NULL CHECK(status IN ('active','disabled','pending')),
 email_verified_at TIMESTAMPTZ, last_login_at TIMESTAMPTZ,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), deleted_at TIMESTAMPTZ
);
CREATE TABLE account.customer_addresses (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 customer_id BIGINT NOT NULL REFERENCES account.customers ON DELETE CASCADE,
 address_type VARCHAR(16) NOT NULL CHECK(address_type IN ('shipping','billing')),
 recipient_name VARCHAR(160) NOT NULL, postal_code VARCHAR(12) NOT NULL, country_code CHAR(2) NOT NULL DEFAULT 'JP',
 prefecture VARCHAR(80) NOT NULL, city VARCHAR(80) NOT NULL, address_line1 VARCHAR(160) NOT NULL,
 address_line2 VARCHAR(160), phone VARCHAR(24), is_default BOOLEAN NOT NULL DEFAULT false,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE catalog.brands (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 name VARCHAR(120) NOT NULL, slug VARCHAR(160) NOT NULL UNIQUE, country_code CHAR(2) NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE catalog.categories (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 parent_id BIGINT REFERENCES catalog.categories ON DELETE RESTRICT,
 name VARCHAR(120) NOT NULL, slug VARCHAR(160) NOT NULL UNIQUE, sort_order INT NOT NULL DEFAULT 0,
 is_active BOOLEAN NOT NULL DEFAULT true, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), CHECK(parent_id <> id)
);
CREATE TABLE catalog.products (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 brand_id BIGINT NOT NULL REFERENCES catalog.brands ON DELETE RESTRICT,
 name VARCHAR(200) NOT NULL, slug VARCHAR(240) NOT NULL UNIQUE, description TEXT,
 status VARCHAR(16) NOT NULL CHECK(status IN ('draft','active','inactive','archived')),
 base_price NUMERIC(12,2) NOT NULL CHECK(base_price>=0), currency CHAR(3) NOT NULL DEFAULT 'JPY',
 published_at TIMESTAMPTZ, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), deleted_at TIMESTAMPTZ
);
CREATE TABLE catalog.product_variants (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 product_id BIGINT NOT NULL REFERENCES catalog.products ON DELETE RESTRICT,
 sku VARCHAR(64) NOT NULL UNIQUE, barcode VARCHAR(32) UNIQUE, name VARCHAR(120) NOT NULL,
 price NUMERIC(12,2) NOT NULL CHECK(price>=0), currency CHAR(3) NOT NULL DEFAULT 'JPY',
 attributes JSONB NOT NULL DEFAULT '{}' CHECK(jsonb_typeof(attributes)='object'),
 weight_grams INT NOT NULL CHECK(weight_grams>=0), is_active BOOLEAN NOT NULL DEFAULT true,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), UNIQUE(id,product_id)
);
CREATE TABLE catalog.product_categories (
 product_id BIGINT NOT NULL REFERENCES catalog.products ON DELETE CASCADE,
 category_id BIGINT NOT NULL REFERENCES catalog.categories ON DELETE RESTRICT,
 PRIMARY KEY(product_id,category_id)
);
CREATE TABLE catalog.product_images (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 product_id BIGINT NOT NULL REFERENCES catalog.products ON DELETE CASCADE, variant_id BIGINT,
 url TEXT NOT NULL, alt_text TEXT NOT NULL, sort_order INT NOT NULL DEFAULT 0,
 is_primary BOOLEAN NOT NULL DEFAULT false, created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 FOREIGN KEY(variant_id,product_id) REFERENCES catalog.product_variants(id,product_id) ON DELETE RESTRICT
);
CREATE TABLE inventory.warehouses (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 code VARCHAR(32) NOT NULL UNIQUE, name VARCHAR(120) NOT NULL, postal_code VARCHAR(12) NOT NULL,
 prefecture VARCHAR(80) NOT NULL, city VARCHAR(80) NOT NULL, address VARCHAR(200) NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE inventory.stocks (
 warehouse_id BIGINT NOT NULL REFERENCES inventory.warehouses ON DELETE RESTRICT,
 variant_id BIGINT NOT NULL REFERENCES catalog.product_variants ON DELETE RESTRICT,
 quantity_on_hand INT NOT NULL DEFAULT 0 CHECK(quantity_on_hand>=0),
 quantity_reserved INT NOT NULL DEFAULT 0 CHECK(quantity_reserved>=0 AND quantity_reserved<=quantity_on_hand),
 reorder_level INT NOT NULL DEFAULT 10 CHECK(reorder_level>=0), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 PRIMARY KEY(warehouse_id,variant_id)
);
CREATE TABLE inventory.stock_movements (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 warehouse_id BIGINT NOT NULL, variant_id BIGINT NOT NULL,
 movement_type VARCHAR(16) NOT NULL CHECK(movement_type IN ('purchase','sale','return','adjustment','reservation','release')),
 quantity INT NOT NULL CHECK(quantity<>0), reference_type VARCHAR(16) NOT NULL CHECK(reference_type IN ('order','purchase','adjustment')),
 reference_id BIGINT, note TEXT, created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 FOREIGN KEY(warehouse_id,variant_id) REFERENCES inventory.stocks ON DELETE RESTRICT,
 CHECK ((movement_type IN ('purchase','return','reservation') AND quantity>0) OR (movement_type IN ('sale','release') AND quantity<0) OR movement_type='adjustment'),
 CHECK(reference_type <> 'order' OR reference_id IS NOT NULL)
);
CREATE TABLE sales.carts (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 customer_id BIGINT NOT NULL REFERENCES account.customers ON DELETE CASCADE,
 status VARCHAR(16) NOT NULL CHECK(status IN ('active','converted','abandoned')),
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), checked_out_at TIMESTAMPTZ,
 CHECK ((status='converted') = (checked_out_at IS NOT NULL))
);
CREATE TABLE sales.cart_items (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, cart_id BIGINT NOT NULL REFERENCES sales.carts ON DELETE CASCADE,
 variant_id BIGINT NOT NULL REFERENCES catalog.product_variants ON DELETE RESTRICT,
 quantity INT NOT NULL CHECK(quantity>0), unit_price NUMERIC(12,2) NOT NULL CHECK(unit_price>=0),
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), UNIQUE(cart_id,variant_id)
);
CREATE TABLE sales.orders (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 order_number VARCHAR(40) NOT NULL UNIQUE, customer_id BIGINT NOT NULL REFERENCES account.customers ON DELETE RESTRICT,
 status VARCHAR(16) NOT NULL CHECK(status IN ('pending','confirmed','paid','processing','shipped','delivered','cancelled','returned')),
 currency CHAR(3) NOT NULL DEFAULT 'JPY', subtotal NUMERIC(12,2) NOT NULL CHECK(subtotal>=0),
 discount_total NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK(discount_total>=0 AND discount_total<=subtotal),
 tax_total NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK(tax_total>=0), shipping_total NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK(shipping_total>=0),
 grand_total NUMERIC(12,2) NOT NULL CHECK(grand_total>=0),
 placed_at TIMESTAMPTZ NOT NULL, paid_at TIMESTAMPTZ, cancelled_at TIMESTAMPTZ, completed_at TIMESTAMPTZ,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK(grand_total=subtotal-discount_total+tax_total+shipping_total), CHECK(paid_at>=placed_at),
 CHECK(cancelled_at>=placed_at), CHECK(completed_at>=paid_at)
);
CREATE TABLE sales.order_items (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, order_id BIGINT NOT NULL REFERENCES sales.orders ON DELETE RESTRICT,
 product_id BIGINT NOT NULL REFERENCES catalog.products ON DELETE RESTRICT, variant_id BIGINT NOT NULL,
 sku VARCHAR(64) NOT NULL, product_name VARCHAR(200) NOT NULL, variant_name VARCHAR(120) NOT NULL,
 quantity INT NOT NULL CHECK(quantity>0), unit_price NUMERIC(12,2) NOT NULL CHECK(unit_price>=0),
 discount_amount NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK(discount_amount>=0 AND discount_amount<=quantity*unit_price),
 tax_amount NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK(tax_amount>=0), line_total NUMERIC(12,2) NOT NULL CHECK(line_total>=0),
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), FOREIGN KEY(variant_id,product_id) REFERENCES catalog.product_variants(id,product_id) ON DELETE RESTRICT,
 CHECK(line_total=quantity*unit_price-discount_amount+tax_amount)
);
CREATE TABLE sales.order_addresses (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, order_id BIGINT NOT NULL REFERENCES sales.orders ON DELETE RESTRICT,
 address_type VARCHAR(16) NOT NULL CHECK(address_type IN ('shipping','billing')),
 recipient_name VARCHAR(160) NOT NULL, postal_code VARCHAR(12) NOT NULL, country_code CHAR(2) NOT NULL,
 prefecture VARCHAR(80) NOT NULL, city VARCHAR(80) NOT NULL, address_line1 VARCHAR(160) NOT NULL,
 address_line2 VARCHAR(160), phone VARCHAR(24), UNIQUE(order_id,address_type)
);
CREATE TABLE sales.order_status_history (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, order_id BIGINT NOT NULL REFERENCES sales.orders ON DELETE RESTRICT,
 from_status VARCHAR(16), to_status VARCHAR(16) NOT NULL, reason TEXT, created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE payment.payments (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 order_id BIGINT NOT NULL REFERENCES sales.orders ON DELETE RESTRICT,
 provider VARCHAR(24) NOT NULL CHECK(provider IN ('stripe','paypal','paypay','card_gateway')),
 provider_transaction_id VARCHAR(120) UNIQUE, method VARCHAR(24) NOT NULL CHECK(method IN ('credit_card','paypal','paypay','bank_transfer')),
 status VARCHAR(24) NOT NULL CHECK(status IN ('pending','authorized','captured','failed','cancelled','refunded','partially_refunded')),
 amount NUMERIC(12,2) NOT NULL CHECK(amount>=0), currency CHAR(3) NOT NULL DEFAULT 'JPY',
 authorized_at TIMESTAMPTZ, captured_at TIMESTAMPTZ, failed_at TIMESTAMPTZ, created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK(authorized_at>=created_at), CHECK(captured_at>=authorized_at), CHECK(failed_at>=created_at),
 CHECK(status NOT IN ('captured','refunded','partially_refunded') OR captured_at IS NOT NULL)
);
CREATE TABLE payment.refunds (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 payment_id BIGINT NOT NULL REFERENCES payment.payments ON DELETE RESTRICT, amount NUMERIC(12,2) NOT NULL CHECK(amount>0),
 reason TEXT NOT NULL, status VARCHAR(16) NOT NULL CHECK(status IN ('pending','completed','failed')),
 provider_refund_id VARCHAR(120) UNIQUE, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), completed_at TIMESTAMPTZ,
 CHECK(completed_at>=created_at), CHECK((status='completed')=(completed_at IS NOT NULL))
);
CREATE TABLE shipping.shipments (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 order_id BIGINT NOT NULL REFERENCES sales.orders ON DELETE RESTRICT,
 warehouse_id BIGINT NOT NULL REFERENCES inventory.warehouses ON DELETE RESTRICT,
 carrier VARCHAR(40) NOT NULL, tracking_number VARCHAR(80) UNIQUE,
 status VARCHAR(16) NOT NULL CHECK(status IN ('pending','ready','shipped','in_transit','delivered','failed','returned')),
 shipped_at TIMESTAMPTZ, delivered_at TIMESTAMPTZ, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK(shipped_at>=created_at), CHECK(delivered_at>=shipped_at), CHECK(delivered_at IS NULL OR shipped_at IS NOT NULL),
 CHECK(status NOT IN ('shipped','in_transit','delivered','returned') OR shipped_at IS NOT NULL),
 CHECK(status NOT IN ('delivered','returned') OR delivered_at IS NOT NULL)
);
CREATE TABLE marketing.coupons (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL, code VARCHAR(40) NOT NULL UNIQUE,
 name VARCHAR(120) NOT NULL, discount_type VARCHAR(16) NOT NULL CHECK(discount_type IN ('percentage','fixed')),
 discount_value NUMERIC(12,2) NOT NULL CHECK(discount_value>0), min_order_amount NUMERIC(12,2) NOT NULL CHECK(min_order_amount>=0),
 max_discount_amount NUMERIC(12,2) CHECK(max_discount_amount>0), usage_limit INT CHECK(usage_limit>0),
 used_count INT NOT NULL DEFAULT 0 CHECK(used_count>=0), starts_at TIMESTAMPTZ NOT NULL, ends_at TIMESTAMPTZ NOT NULL,
 is_active BOOLEAN NOT NULL DEFAULT true, created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK(ends_at>starts_at), CHECK(discount_type<>'percentage' OR discount_value<=100), CHECK(used_count<=usage_limit)
);
CREATE TABLE marketing.coupon_redemptions (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, coupon_id BIGINT NOT NULL REFERENCES marketing.coupons ON DELETE RESTRICT,
 customer_id BIGINT NOT NULL REFERENCES account.customers ON DELETE RESTRICT,
 order_id BIGINT NOT NULL UNIQUE REFERENCES sales.orders ON DELETE RESTRICT,
 discount_amount NUMERIC(12,2) NOT NULL CHECK(discount_amount>0), redeemed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE review.product_reviews (
 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, public_id UUID NOT NULL,
 customer_id BIGINT NOT NULL REFERENCES account.customers ON DELETE RESTRICT,
 product_id BIGINT NOT NULL REFERENCES catalog.products ON DELETE RESTRICT,
 order_item_id BIGINT UNIQUE REFERENCES sales.order_items ON DELETE RESTRICT,
 rating SMALLINT NOT NULL CHECK(rating BETWEEN 1 AND 5), title VARCHAR(160), content TEXT,
 is_verified_purchase BOOLEAN NOT NULL DEFAULT false,
 status VARCHAR(16) NOT NULL CHECK(status IN ('pending','published','rejected')),
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK(NOT is_verified_purchase OR order_item_id IS NOT NULL)
);
