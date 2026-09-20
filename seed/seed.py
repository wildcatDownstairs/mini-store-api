"""Deterministic correlated e-commerce data, bounded COPY batches, integer JPY arithmetic."""
from bisect import bisect_right
from collections import defaultdict
from datetime import datetime, timedelta, timezone
import heapq
import math
import os
from pathlib import Path
import random
import sys
import uuid

from faker import Faker
from psycopg import sql
from psycopg.types.json import Jsonb

sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'scripts'))
from manage_db import ROOT, TABLES, assert_layout, assert_owned, connect

SCALES = {'small': (2000,500,1200,5000,30,90,4),
          'medium': (20000,5000,12000,100000,150,90,4),
          'large': (100000,25000,60000,500000,750,450,20)}
LOCATIONS = [('東京都','江東区','135-0061','豊洲'),('大阪府','大阪市北区','530-0001','梅田'),
             ('愛知県','名古屋市中村区','450-0002','名駅'),('福岡県','福岡市博多区','812-0011','博多駅前'),
             ('神奈川県','横浜市西区','220-0012','みなとみらい'),('北海道','札幌市中央区','060-0042','大通西'),
             ('京都府','京都市下京区','600-8001','真町'),('宮城県','仙台市青葉区','980-0021','中央')]
# name, price range, tax percent, typical stock, variant kind
GROUPS = [('Electronics',8000,65000,10,35,'tech'),('Computers',65000,250000,10,18,'tech'),
          ('Fashion',1800,18000,10,90,'fashion'),('Home',1200,28000,10,65,'home'),
          ('Beauty',800,12000,10,130,'beauty'),('Food',300,6000,8,240,'food')]
NAMES = [['ワイヤレスイヤホン','スマートフォン','ポータブルスピーカー','スマートウォッチ'],
         ['薄型ノートパソコン','クリエイター向けミニPC','モバイルノートPC','高性能デスクトップPC'],
         ['コットンシャツ','リネンワンピース','ストレッチパンツ','軽量スニーカー'],
         ['木製サイドテーブル','調光デスクライト','ステンレスケトル','リネン寝具セット'],
         ['保湿美容液','低刺激洗顔フォーム','日焼け止めクリーム','ボタニカルシャンプー'],
         ['宇治抹茶クッキー','北海道ブレンド珈琲','国産雑穀米','瀬戸内レモン紅茶']]


def run(reset=False):
    scale = os.getenv('SEED_SCALE','medium')
    if scale not in SCALES: raise ValueError('SEED_SCALE must be small, medium, or large')
    nc,np,nv,no,nb,ncat,nw = SCALES[scale]
    seed = int(os.getenv('SEED','20260918'))
    rng = random.Random(seed)
    ids_rng = random.Random(seed+1)
    fake = Faker('ja_JP'); fake.seed_instance(seed)
    anchor = datetime.fromisoformat(os.getenv('SEED_AS_OF','2026-09-18T00:00:00+00:00'))
    if anchor.tzinfo is None: raise ValueError('SEED_AS_OF requires timezone')
    start = anchor-timedelta(days=1095)
    jst = timezone(timedelta(hours=9))
    rows = defaultdict(list); counters = defaultdict(int); columns = {}

    with connect() as conn:
        assert_owned(conn); assert_layout(conn)
        conn.execute('SELECT pg_advisory_xact_lock(20260918,3)')
        # No CASCADE: any unrecognized referencing data prevents reset; failure rolls back all data.
        conn.execute(sql.SQL('LOCK TABLE {} IN ACCESS EXCLUSIVE MODE').format(sql.SQL(',').join(sql.Identifier(*t.split('.')) for t in TABLES)))
        if reset:
            conn.execute(sql.SQL('TRUNCATE {} RESTART IDENTITY RESTRICT').format(sql.SQL(',').join(sql.Identifier(*t.split('.')) for t in TABLES)))
        elif any(conn.execute(sql.SQL('SELECT EXISTS(SELECT 1 FROM {})').format(sql.Identifier(*t.split('.')))).fetchone()[0] for t in TABLES):
            raise RuntimeError('Lab already contains data; use reset_db explicitly; nothing changed')
        metadata = conn.execute("SELECT table_schema||'.'||table_name,column_name FROM information_schema.columns WHERE column_name='public_id'").fetchall()
        public_tables = {t for t,c in metadata}
        v7 = conn.execute("SELECT to_regprocedure('pg_catalog.uuidv7()') IS NOT NULL").fetchone()[0]

        def add(table, **row):
            if table not in ('catalog.product_categories','inventory.stocks'):
                counters[table]+=1; row={'id':counters[table],**row}
            if table in public_tables:
                stamp = row.get('created_at',start)
                if v7:
                    value = (int(stamp.timestamp()*1000)<<80) | (7<<76) | (ids_rng.getrandbits(12)<<64) | (2<<62) | ids_rng.getrandbits(62)
                    row['public_id']=uuid.UUID(int=value)
                else: row['public_id']=uuid.UUID(int=ids_rng.getrandbits(128),version=4)
            keys=tuple(row)
            if table in columns and columns[table]!=keys: raise AssertionError(f'column mismatch {table}')
            columns[table]=keys; rows[table].append(tuple(row.values()))
            return row.get('id')

        def flush():
            for table in [t for t in TABLES if t!='inventory.stock_movements']+['inventory.stock_movements']:
                if not rows[table]: continue
                query=sql.SQL('COPY {} ({}) FROM STDIN').format(sql.Identifier(*table.split('.')),sql.SQL(',').join(map(sql.Identifier,columns[table])))
                with conn.cursor().copy(query) as cp:
                    for row in rows[table]: cp.write_row(row)
                rows[table].clear()

        # Registration dates sorted for efficient eligible-customer sampling.
        customer_times=sorted(start+timedelta(seconds=rng.random()**0.65*(anchor-start).total_seconds()) for _ in range(nc))
        customer_times[0]=start
        customers=[]
        for i,created in enumerate(customer_times,1):
            last,first=fake.last_name(),fake.first_name()
            phone=f'0{rng.choice([70,80,90])}{rng.randrange(10**8):08d}'
            loc=rng.choice(LOCATIONS)
            address=dict(recipient_name=last+' '+first,postal_code=loc[2],country_code='JP',prefecture=loc[0],city=loc[1],address_line1=f'{loc[3]} {rng.randint(1,5)}-{rng.randint(1,28)}-{rng.randint(1,16)}',address_line2=f'サクラレジデンス {rng.randint(101,1208)}' if rng.random()<.6 else None,phone=phone)
            customers.append(address)
            status=rng.choices(['active','pending','disabled'],[94,4,2])[0]
            add('account.customers',email=f'{fake.romanized_name().lower().replace(" ",".")}.{i}@{rng.choice(["sakura.example","mail.example","hikari.example"])}',password_hash='$argon2id$v=19$m=65536,t=3,p=1$LAB_ONLY_NOT_A_LOGIN$'+f'{ids_rng.getrandbits(96):024x}',first_name=first,last_name=last,phone=phone,birth_date=(anchor-timedelta(days=rng.randint(18*366,78*365))).date(),status=status,email_verified_at=created+timedelta(minutes=2) if status!='pending' and created<anchor-timedelta(minutes=2) else None,last_login_at=created+(anchor-created)*rng.random(),created_at=created,updated_at=created,deleted_at=None)
            add('account.customer_addresses',customer_id=i,address_type='shipping',**address,is_default=True,created_at=created,updated_at=created)
            if i%2==0:
                second={**address,'address_line2':None}
                add('account.customer_addresses',customer_id=i,address_type='billing',**second,is_default=True,created_at=created,updated_at=created)
            if i%2000==0: flush()
        flush(); print('customers done',flush=True)
        brand_names=[]
        for i in range(nb):
            name=f'{["Sora","Hikari","Mori","Nami","Kumo","Aoba","Tsuki","Yui","Haru","Kaze"][i%10]} {["Works","Living","Studio","Craft","Select","Supply","Atelier","Labs","House","Market","Life","Design","Field","Nest","Collective"][i//10%15]}'
            if i>=150: name+=f' {i//150+1}'
            brand_names.append(name)
            add('catalog.brands',name=name,slug=f'{name.lower().replace(" ","-")}',country_code=rng.choices(['JP','US','DE','KR'],[85,5,5,5])[0],created_at=start)
        for i in range(ncat):
            root=i<6; group=i if root else (i-6)%6
            labels=['Smartphones','Tablets','Audio','Wearables','Accessories','Men','Women','Kitchen','Skincare','Coffee','Storage','Premium','Daily','Travel']
            add('catalog.categories',parent_id=None if root else group+1,name=GROUPS[group][0] if root else GROUPS[group][0]+' / '+labels[(i-6)//6%len(labels)],slug=f'category-{i+1}',sort_order=i,is_active=True,created_at=start)
        variants=[]; purchasable=[]
        for i in range(1,np+1):
            group=(i-1)%6; g=GROUPS[group]; brand=rng.randrange(nb)
            kind=rng.choice(NAMES[group])
            low,high=({'ワイヤレスイヤホン':(4000,35000),'スマートフォン':(28000,150000),'ポータブルスピーカー':(3500,28000),'スマートウォッチ':(8000,65000)}.get(kind,(g[1],g[2])))
            price=rng.randrange(low//100,high//100+1)*100
            name=f'{brand_names[brand]} {kind} {rng.choice(["風","凪","光","彩","結","晴"])}-{i%300+1:03d}'
            created=start+timedelta(days=rng.randrange(30))
            product_status=rng.choices(['active','inactive','archived','draft'],[90,5,3,2])[0]
            add('catalog.products',brand_id=brand+1,name=name,slug=f'{g[5]}-{i}-collection',description=f'{name}。毎日の暮らしに合わせて使いやすさと品質を追求したオリジナルモデル。素材・寸法はバリエーションをご確認ください。',status=product_status,base_price=price,currency='JPY',published_at=created if product_status!='draft' else None,created_at=created,updated_at=created,deleted_at=None)
            leaves=[j+1 for j in range(6,ncat) if (j-6)%6==group]
            add('catalog.product_categories',product_id=i,category_id=rng.choice(leaves))
            if i%5==0: add('catalog.product_categories',product_id=i,category_id=group+1)
            add('catalog.product_images',product_id=i,variant_id=None,url=f'https://images.example/mini-store/{i}/main.webp',alt_text=name,sort_order=0,is_primary=True,created_at=created)
            for k in range(nv//np+(1 if i<=nv%np else 0)):
                if group==1 or kind=='スマートフォン': attrs={'color':['Black','White','Silver'][k],'storage':['128GB','256GB','512GB'][k]}
                elif group==0: attrs={'color':['Black','White','Silver'][k]}
                elif group==2: attrs={'color':['Navy','Ivory','Black'][k],'size':['S','M','L'][k]}
                elif group==5: attrs={'pack':['200g','400g','600g'][k]}
                elif group==4: attrs={'volume':['100ml','150ml','200ml'][k]}
                else: attrs={'color':['Natural','White','Walnut'][k]}
                variant_name=' / '.join(attrs.values()); vp=price+(price*k//10//10)*10
                vid=len(variants)+1; sku=f'MS-{group+1:02d}-{i:06d}-{k+1:02d}'
                raw=f'29{vid:010d}'; check=(10-sum(int(c)*(1 if n%2==0 else 3) for n,c in enumerate(raw))%10)%10
                add('catalog.product_variants',product_id=i,sku=sku,barcode=raw+str(check),name=variant_name,price=vp,currency='JPY',attributes=Jsonb(attrs),weight_grams=rng.randint(80,600) if group!=1 else rng.randint(1000,3000),is_active=True,created_at=created,updated_at=created)
                variants.append((i,sku,name,variant_name,vp,g[4],g[3],created))
                if product_status!='draft': purchasable.append(vid)
        flush(); print('products done',flush=True)
        balances={}; events=[]; event_serial=0
        for w in range(1,nw+1):
            loc=LOCATIONS[(w-1)%4]
            add('inventory.warehouses',code=f'JP-{["TYO","OSA","NGO","FUK"][(w-1)%4]}-{(w-1)//4+1}',name=f'{loc[1]} 物流センター {(w-1)//4+1}',postal_code=loc[2],prefecture=loc[0],city=loc[1],address=loc[3]+' 1-2-3',created_at=start)
            for v in range(1,nv+1):
                typical=variants[v-1][5]
                amount=0 if rng.random()<.03 else rng.randint(typical//4,typical)
                balances[w,v]=[amount,0]
                add('inventory.stocks',warehouse_id=w,variant_id=v,quantity_on_hand=amount,quantity_reserved=0,reorder_level=max(3,typical//5),updated_at=start)
                if amount: add('inventory.stock_movements',warehouse_id=w,variant_id=v,movement_type='purchase',quantity=amount,reference_type='purchase',reference_id=None,note='初期仕入れ',created_at=start+timedelta(days=30))
            flush()
        coupon_count=12 if scale!='large' else 60
        for i in range(coupon_count):
            add('marketing.coupons',code=f'{["WELCOME","WEEKEND","SEASON","THANKYOU"][i%4]}-{i+1:02d}',name='会員限定 10% OFF' if i%2==0 else '会員限定 500円 OFF',discount_type='percentage' if i%2==0 else 'fixed',discount_value=10 if i%2==0 else 500,min_order_amount=3000,max_discount_amount=2000,usage_limit=no,used_count=0,starts_at=start,ends_at=anchor+timedelta(days=30),is_active=True,created_at=start)
        # A modest cart sample; converted carts refer to actual orders generated below.
        for i in range(1,nc//3+1):
            created=max(customer_times[i-1],anchor-timedelta(days=rng.randint(1,45)))
            cart=add('sales.carts',customer_id=i,status='active' if i%3 else 'abandoned',created_at=created,updated_at=created,checked_out_at=None)
            for v in rng.sample(range(1,nv+1),rng.randint(1,3)):
                add('sales.cart_items',cart_id=cart,variant_id=v,quantity=1,unit_price=variants[v-1][4],created_at=created,updated_at=created)
        flush()
        days=[]; weights=[]
        for d in range(40,1095):
            date=(start+timedelta(days=d)).astimezone(jst)
            promo=(date.month==11 and 22<=date.day<=30) or (date.month==12 and date.day>=20) or (date.month==1 and date.day<=7) or (date.month==5 and date.day<=7) or (date.month==7 and 10<=date.day<=17)
            days.append(date.replace(hour=0,minute=0,second=0,microsecond=0))
            weights.append(math.exp(3*d/1095)*(1.18 if date.weekday()>=5 else 1)*(3.5 if promo else 1))
        order_dates=[]
        for day in rng.choices(days,weights,k=no):
            hour=rng.choices(range(24),[1]*8+[2]*10+[7]*6)[0]
            placed=day+timedelta(hours=hour,minutes=rng.randrange(60),seconds=rng.randrange(60))
            order_dates.append(min(placed,anchor-timedelta(minutes=20)))
        order_dates.sort()

        def movement(w,v,kind,q,oid,at):
            balance=balances[w,v]
            balance[1 if kind in ('reservation','release') else 0]+=q
            if not (0<=balance[1]<=balance[0]): raise AssertionError('stock timeline broken')
            add('inventory.stock_movements',warehouse_id=w,variant_id=v,movement_type=kind,quantity=q,reference_type='order',reference_id=oid,note={'purchase':'補充仕入れ','reservation':'注文引当','release':'出庫引当解除','sale':'注文出庫','return':'返品入庫'}[kind],created_at=at)

        def drain(until):
            while events and events[0][0]<=until:
                at,serial,w,v,kind,q,oid=heapq.heappop(events)
                movement(w,v,kind,q,oid,at)

        for oid,placed in enumerate(order_dates,1):
            drain(placed)
            eligible=bisect_right(customer_times,placed-timedelta(minutes=5))
            customer=1+int(rng.random()**1.6*eligible)
            state=rng.choices(['pending','confirmed','paid','processing','shipped','delivered','cancelled','returned'],[1,2,4,6,5,75,3,4])[0]
            age=(anchor-placed).total_seconds()/86400
            if age>14 and state in ('pending','confirmed','paid','processing','shipped'):
                state=rng.choices(['delivered','cancelled'],[85,15])[0]
            if age<12: state=rng.choices(['pending','confirmed','paid','processing','shipped','cancelled'],[10,10,20,25,30 if age>4 else 0,5])[0]
            paid=placed+timedelta(minutes=3) if state in ('paid','processing','shipped','delivered','returned') else None
            shipped=placed+timedelta(days=1,hours=2) if state in ('shipped','delivered','returned') else None
            delivered=shipped+timedelta(days=2) if state in ('delivered','returned') else None
            returned=delivered+timedelta(days=4) if state=='returned' else None
            completed=delivered
            updated=returned or delivered or shipped or (placed+timedelta(minutes=10) if state=='processing' else paid) or placed+timedelta(minutes=2)
            nitems=rng.choices([1,2,3,4,5,6],[10,20,35,20,10,5])[0]
            chosen=set()
            while len(chosen)<nitems: chosen.add(purchasable[min(len(purchasable)-1,int(rng.random()**2.2*len(purchasable)))])
            details=[]
            for v in sorted(chosen):
                qty=rng.choices([1,2,3],[80,17,3])[0]; variant=variants[v-1]
                details.append([v,qty,variant[4],0,0])
            subtotal=sum(q*p for v,q,p,d,t in details)
            coupon=rng.randrange(coupon_count) if subtotal>=3000 and rng.random()<.23 else None
            discount=min(2000,subtotal//10) if coupon is not None and coupon%2==0 else (500 if coupon is not None else 0)
            remaining=discount
            for n,line in enumerate(details):
                v,q,p,_,_=line
                d=remaining if n==len(details)-1 else discount*(q*p)//subtotal
                remaining-=d; rate=variants[v-1][6]
                line[3]=d; line[4]=((q*p-d)*rate+50)//100
            tax=sum(x[4] for x in details); freight=0 if subtotal-discount>=5000 else 550
            grand=subtotal-discount+tax+freight
            add('sales.orders',order_number=f'JP-{placed:%Y%m%d}-{oid:09d}',customer_id=customer,status=state,currency='JPY',subtotal=subtotal,discount_total=discount,tax_total=tax,shipping_total=freight,grand_total=grand,placed_at=placed,paid_at=paid,cancelled_at=placed+timedelta(minutes=2) if state=='cancelled' else None,completed_at=completed,created_at=placed,updated_at=updated)
            if coupon is not None:
                add('marketing.coupon_redemptions',coupon_id=coupon+1,customer_id=customer,order_id=oid,discount_amount=discount,redeemed_at=placed)
            for atype in ('shipping','billing'): add('sales.order_addresses',order_id=oid,address_type=atype,**customers[customer-1])
            stages=[('pending',placed)]
            if state!='pending': stages.append(('confirmed',placed+timedelta(minutes=1)))
            if state=='cancelled': stages.append(('cancelled',placed+timedelta(minutes=2)))
            if paid: stages.append(('paid',paid))
            if state in ('processing','shipped','delivered','returned'): stages.append(('processing',placed+timedelta(minutes=10)))
            if shipped: stages.append(('shipped',shipped))
            if delivered: stages.append(('delivered',delivered))
            if returned: stages.append(('returned',returned))
            previous=None
            for stage,at in stages:
                add('sales.order_status_history',order_id=oid,from_status=previous,to_status=stage,reason={'pending':'注文受付','confirmed':'注文確認','paid':'決済確定','processing':'出荷準備','shipped':'出荷完了','delivered':'配達完了','returned':'返品受付','cancelled':'お客様のご都合'}[stage],created_at=at)
                previous=stage
            if state!='pending':
                provider,method=rng.choice([('stripe','credit_card'),('card_gateway','credit_card'),('paypay','paypay'),('paypal','paypal'),('card_gateway','bank_transfer')])
                if paid and rng.random()<.04:
                    add('payment.payments',order_id=oid,provider=provider,provider_transaction_id=f'lab_failed_{oid}',method=method,status='failed',amount=grand,currency='JPY',authorized_at=None,captured_at=None,failed_at=placed+timedelta(seconds=90),created_at=placed+timedelta(seconds=30))
                partial=state=='delivered' and rng.random()<.018
                ps='refunded' if returned else ('partially_refunded' if partial else ('captured' if paid else ('cancelled' if state=='cancelled' else 'authorized')))
                pid=add('payment.payments',order_id=oid,provider=provider,provider_transaction_id=f'lab_{provider}_{oid:010d}',method=method,status=ps,amount=grand,currency='JPY',authorized_at=placed+timedelta(minutes=2) if state!='cancelled' else None,captured_at=paid,failed_at=None,created_at=placed+timedelta(minutes=1))
                if returned or partial:
                    at=returned or delivered+timedelta(days=2)
                    add('payment.refunds',payment_id=pid,amount=grand if returned else max(1,grand//10),reason='商品返品' if returned else '配送遅延のお詫び',status='completed',provider_refund_id=f'lab_refund_{oid:010d}',created_at=at,completed_at=at+timedelta(hours=2))
            warehouse=1+(LOCATIONS.index(next(loc for loc in LOCATIONS if loc[0]==customers[customer-1]['prefecture']))%nw)
            if nw>4: warehouse=1+(warehouse-1)%4+4*rng.randrange(nw//4)
            if paid:
                add('shipping.shipments',order_id=oid,warehouse_id=warehouse,carrier=rng.choices(['Yamato','Sagawa','Japan Post'],[50,30,20])[0],tracking_number=f'{100000000000+oid:012d}',status='returned' if returned else ('delivered' if delivered else ('in_transit' if shipped else ('ready' if state=='processing' else 'pending'))),shipped_at=shipped,delivered_at=delivered,created_at=paid,updated_at=updated)
            for v,q,p,d,t in details:
                product,sku,name,vname,*_=variants[v-1]
                item=add('sales.order_items',order_id=oid,product_id=product,variant_id=v,sku=sku,product_name=name,variant_name=vname,quantity=q,unit_price=p,discount_amount=d,tax_amount=t,line_total=q*p-d+t,created_at=placed)
                if paid:
                    balance=balances[warehouse,v]
                    if balance[0]-balance[1]<q:
                        movement(warehouse,v,'purchase',max(q,variants[v-1][5]),oid,placed-timedelta(microseconds=1))
                    movement(warehouse,v,'reservation',q,oid,placed)
                    for kind,quantity,at in [('release',-q,shipped),('sale',-q,shipped),('return',q,returned)]:
                        if at:
                            event_serial+=1; heapq.heappush(events,(at,event_serial,warehouse,v,kind,quantity,oid))
                if state=='delivered' and rng.random()<.23:
                    rating=rng.choices([5,4,3,2,1],[55,27,10,5,3])[0]
                    text=rng.random()>.08; at=delivered+timedelta(days=rng.randint(1,7),hours=1)
                    add('review.product_reviews',customer_id=customer,product_id=product,order_item_id=item,rating=rating,title={5:'買ってよかったです',4:'使いやすいです',3:'期待どおり',2:'少し残念でした',1:'期待と違いました'}[rating] if text else None,content=rng.choice(['梱包が丁寧で、予定どおり届きました。','毎日使っています。サイズもちょうどよかったです。','商品説明をもう少し詳しくしてほしいです。']) if text else None,is_verified_purchase=True,status=rng.choices(['published','pending','rejected'],[96,3,1])[0],created_at=at,updated_at=at)
            if oid%20==0:
                cart=add('sales.carts',customer_id=customer,status='converted',created_at=placed-timedelta(minutes=3),updated_at=placed,checked_out_at=placed)
                for v,q,p,d,t in details: add('sales.cart_items',cart_id=cart,variant_id=v,quantity=q,unit_price=p,created_at=placed-timedelta(minutes=3),updated_at=placed)
            if oid%2000==0: flush()
            if oid%max(1,no//10)==0: print(f'orders {oid*100//no}%',flush=True)
        drain(anchor); assert not events
        flush()
        # Final balances imported in one COPY, not one UPDATE per stock row.
        conn.execute('CREATE TEMP TABLE final_stocks (warehouse_id bigint,variant_id bigint,on_hand int,reserved int) ON COMMIT DROP')
        with conn.cursor().copy('COPY final_stocks FROM STDIN') as cp:
            for (w,v),(hand,reserved) in balances.items(): cp.write_row((w,v,hand,reserved))
        conn.execute('UPDATE inventory.stocks s SET quantity_on_hand=f.on_hand,quantity_reserved=f.reserved,updated_at=%s FROM final_stocks f WHERE s.warehouse_id=f.warehouse_id AND s.variant_id=f.variant_id',(anchor,))
        conn.execute('UPDATE marketing.coupons c SET used_count=(SELECT count(*) FROM marketing.coupon_redemptions r WHERE r.coupon_id=c.id)')
        for table in counters:
            conn.execute(sql.SQL("SELECT setval(pg_get_serial_sequence(%s,'id'),coalesce(max(id),1),count(*)>0) FROM {}").format(sql.Identifier(*table.split('.'))),(table,))
        conn.execute('ANALYZE')
        conn.execute((ROOT/'db/08_verify.sql').read_text())
        print('verification passed; committing',flush=True)
    with connect(autocommit=True) as conn:
        conn.execute('ANALYZE')
        report(conn,scale,seed,anchor)


def report(conn,scale,seed,anchor):
    out=[f'# Verified dataset\n\nScale: {scale}; seed: {seed}; as of: {anchor.isoformat()}\n', '| Table | Rows |','|---|---:|']
    total=0
    for table in TABLES:
        count=conn.execute(sql.SQL('SELECT count(*) FROM {}').format(sql.Identifier(*table.split('.')))).fetchone()[0]
        total+=count; out.append(f'| {table} | {count:,} |')
    size=conn.execute("SELECT pg_size_pretty(pg_database_size('ecommerce_lab'))").fetchone()[0]
    out.extend([f'\nTotal rows: **{total:,}**; database size: **{size}**.\n'])
    print('\n'.join(out),flush=True)
    for table,fields in [('account.customers','id,last_name,first_name,email'),('catalog.products','id,name,base_price,currency'),('sales.orders','order_number,status,subtotal,discount_total,tax_total,shipping_total,grand_total'),('sales.order_items','order_id,product_name,variant_name,quantity,unit_price,line_total'),('payment.payments','order_id,provider,method,status,amount')]:
        cur=conn.execute(sql.SQL('SELECT '+fields+' FROM {} ORDER BY md5(id::text || %s) LIMIT 5').format(sql.Identifier(*table.split('.'))),(str(seed),))
        out.extend(['\n## '+table,'','| '+' | '.join(c.name for c in cur.description)+' |','|'+'---|'*len(cur.description)])
        for row in cur.fetchall(): out.append('| '+' | '.join(str(v) for v in row)+' |')
    (ROOT/'docs/seed_report.md').write_text('\n'.join(out)+'\n')
    print('Random samples saved to docs/seed_report.md',flush=True)


if __name__=='__main__':
    run(reset='--reset' in sys.argv)
