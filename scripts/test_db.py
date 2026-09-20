"""Run with .venv/bin/python scripts/test_db.py. Every write is rolled back."""
import psycopg
from manage_db import connect, assert_owned


def main():
    passed=[]
    with connect() as conn:
        assert_owned(conn)
        def rejected(name,statement,params=()):
            try:
                with conn.transaction():
                    conn.execute(statement,params)
            except (psycopg.errors.CheckViolation, psycopg.errors.UniqueViolation, psycopg.errors.ForeignKeyViolation, psycopg.errors.RestrictViolation, psycopg.errors.RaiseException):
                passed.append(name)
            else:
                raise AssertionError(f'Invalid write accepted: {name}')
        email=conn.execute('SELECT id,email FROM account.customers ORDER BY id LIMIT 2').fetchall()
        rejected('email case insensitive','UPDATE account.customers SET email=upper(%s) WHERE id=%s',(email[0][1],email[1][0]))
        rejected('negative price','UPDATE catalog.products SET base_price=-1 WHERE id=1')
        rejected('stock over-reserved','UPDATE inventory.stocks SET quantity_reserved=quantity_on_hand+1 WHERE warehouse_id=1 AND variant_id=1')
        rejected('order formula','UPDATE sales.orders SET grand_total=grand_total+1 WHERE id=1')
        rejected('FK orphan','UPDATE sales.orders SET customer_id=9223372036854775807 WHERE id=1')
        rejected('preserve historical product','DELETE FROM catalog.products WHERE id=(SELECT product_id FROM sales.order_items LIMIT 1)')
        rejected('invalid initial status',"UPDATE sales.order_status_history SET from_status=NULL,to_status='delivered' WHERE id=1")
        rejected('category cycle','UPDATE catalog.categories SET parent_id=(SELECT id FROM catalog.categories WHERE parent_id=1 LIMIT 1) WHERE id=1')
        rejected('refund overpayment',"UPDATE payment.refunds r SET amount=p.amount+1 FROM payment.payments p WHERE r.payment_id=p.id AND r.id=(SELECT min(id) FROM payment.refunds)")
        rejected('review before delivery',"UPDATE review.product_reviews SET created_at='2000-01-01T00:00:00Z' WHERE id=(SELECT min(id) FROM review.product_reviews)")
        rejected('review mismatched customer','UPDATE review.product_reviews SET customer_id=CASE WHEN customer_id=1 THEN 2 ELSE 1 END WHERE id=(SELECT min(id) FROM review.product_reviews)')
        rejected('duplicate active cart',"INSERT INTO sales.carts(customer_id,status) SELECT customer_id,'active' FROM sales.carts WHERE status='active' LIMIT 1")
        # Whole order already reserved: a second call must not duplicate its inventory claim.
        w,v,o=conn.execute("SELECT m.warehouse_id,m.variant_id,m.order_id FROM inventory.stock_movements m JOIN sales.orders o ON o.id=m.order_id WHERE o.status='paid' AND m.movement_type='reservation' LIMIT 1").fetchone()
        rejected('duplicate inventory reservation','SELECT inventory.reserve_stock(%s,%s,1,%s)',(w,v,o))
        # Successful reservation against a confirmed order, rolled back at the end.
        w,v,o=conn.execute("SELECT s.warehouse_id,i.variant_id,o.id FROM sales.orders o JOIN sales.order_items i ON i.order_id=o.id JOIN inventory.stocks s ON s.variant_id=i.variant_id WHERE o.status='confirmed' AND s.quantity_on_hand>s.quantity_reserved LIMIT 1").fetchone()
        before=conn.execute('SELECT quantity_reserved FROM inventory.stocks WHERE warehouse_id=%s AND variant_id=%s',(w,v)).fetchone()[0]
        conn.execute('SELECT inventory.reserve_stock(%s,%s,1,%s)',(w,v,o))
        after=conn.execute('SELECT quantity_reserved FROM inventory.stocks WHERE warehouse_id=%s AND variant_id=%s',(w,v)).fetchone()[0]
        assert after==before+1
        passed.append('atomic reservation success')
        uuid_value=conn.execute("INSERT INTO catalog.brands(name,slug,country_code) VALUES('Rollback check','rollback-check-only','JP') RETURNING public_id").fetchone()[0]
        assert uuid_value.version==(7 if conn.info.server_version>=180000 else 4)
        passed.append('UUID server default')
        # Identity sequence is deliberately not reset after rollback; gaps are normal.
        conn.rollback()
    print(f'{len(passed)} checks passed; all writes rolled back')
    for name in passed: print('PASS',name)


if __name__=='__main__': main()
