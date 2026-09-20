# Verified dataset

Scale: medium; seed: 20260918; as of: 2026-09-18T00:00:00+00:00

| Table | Rows |
|---|---:|
| account.customers | 20,000 |
| account.customer_addresses | 30,000 |
| catalog.brands | 150 |
| catalog.categories | 90 |
| catalog.products | 5,000 |
| catalog.product_variants | 12,000 |
| catalog.product_categories | 6,000 |
| catalog.product_images | 5,000 |
| inventory.warehouses | 4 |
| inventory.stocks | 48,000 |
| inventory.stock_movements | 940,372 |
| sales.carts | 11,666 |
| sales.cart_items | 29,009 |
| sales.orders | 100,000 |
| sales.order_items | 315,102 |
| sales.order_addresses | 200,000 |
| sales.order_status_history | 580,073 |
| payment.payments | 103,426 |
| payment.refunds | 5,487 |
| shipping.shipments | 93,675 |
| marketing.coupons | 12 |
| marketing.coupon_redemptions | 22,808 |
| review.product_reviews | 63,641 |

Total rows: **2,591,515**; database size: **605 MB**.


## account.customers

| id | last_name | first_name | email |
|---|---|---|---|
| 9045 | 藤原 | 和也 | akemi.watanabe.9045@sakura.example |
| 1229 | 渡辺 | 翔太 | kazuya.yoshida.1229@hikari.example |
| 17367 | 後藤 | 亮介 | kenichi.fukuda.17367@hikari.example |
| 7180 | 伊藤 | 里佳 | maaya.kimura.7180@mail.example |
| 10237 | 鈴木 | 稔 | rika.tanaka.10237@hikari.example |

## catalog.products

| id | name | base_price | currency |
|---|---|---|---|
| 1229 | Mori Labs ボタニカルシャンプー 光-030 | 8800.00 | JPY |
| 1872 | Nami Market 瀬戸内レモン紅茶 凪-073 | 2600.00 | JPY |
| 1722 | Kaze Design 国産雑穀米 彩-223 | 2100.00 | JPY |
| 3166 | Kumo Market リネン寝具セット 凪-167 | 7800.00 | JPY |
| 4815 | Kaze Atelier ストレッチパンツ 結-016 | 3200.00 | JPY |

## sales.orders

| order_number | status | subtotal | discount_total | tax_total | shipping_total | grand_total |
|---|---|---|---|---|---|---|
| JP-20250903-000032247 | delivered | 117100.00 | 0.00 | 11710.00 | 0.00 | 128810.00 |
| JP-20241005-000009045 | delivered | 84150.00 | 0.00 | 8290.00 | 0.00 | 92440.00 |
| JP-20251230-000048558 | delivered | 54380.00 | 2000.00 | 5229.00 | 0.00 | 57609.00 |
| JP-20260625-000079260 | delivered | 550.00 | 0.00 | 44.00 | 550.00 | 1144.00 |
| JP-20260427-000066677 | delivered | 35450.00 | 0.00 | 3424.00 | 0.00 | 38874.00 |

## sales.order_items

| order_id | product_name | variant_name | quantity | unit_price | line_total |
|---|---|---|---|---|---|
| 10216 | Mori Studio 調光デスクライト 彩-053 | White | 1 | 9680.00 | 10648.00 |
| 66563 | Kaze Studio 木製サイドテーブル 晴-059 | Walnut | 1 | 9000.00 | 9900.00 |
| 47075 | Kumo Market ストレッチパンツ 結-094 | Ivory / M | 2 | 18920.00 | 41624.00 |
| 85300 | Nami Living 薄型ノートパソコン 晴-183 | White / 256GB | 1 | 269830.00 | 296813.00 |
| 99095 | Yui Design 瀬戸内レモン紅茶 光-265 | 200g | 1 | 4300.00 | 4644.00 |

## payment.payments

| order_id | provider | method | status | amount |
|---|---|---|---|---|
| 31066 | stripe | credit_card | captured | 372060.00 |
| 8731 | stripe | credit_card | captured | 49201.00 |
| 46751 | paypal | paypal | failed | 225500.00 |
| 76354 | stripe | credit_card | captured | 461870.00 |
| 64238 | paypay | paypay | cancelled | 687082.00 |
