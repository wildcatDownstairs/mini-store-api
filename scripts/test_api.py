"""真实 PostgreSQL + HTTP 集成检查。仅创建/清理本次随机命名的隔离库，绝不重置 ecommerce_lab。

运行：.venv/bin/python scripts/test_api.py。凭据随机生成，只在进程内使用。
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
import urllib.request
import urllib.error
import uuid
import psycopg
from psycopg import sql
from run_api import ROOT, dotnet

name = 'ecommerce_lab_api_test_' + uuid.uuid4().hex[:12]
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
base = f'http://127.0.0.1:{port}'
env = {**os.environ, 'PGHOST': os.getenv('PGHOST', '/tmp'), 'PGDATABASE': name,
       'Auth__SigningKey': secrets.token_urlsafe(48), 'ASPNETCORE_ENVIRONMENT': 'Development',
       'ASPNETCORE_URLS': base, 'Features__SimulatedPayments': 'true'}
# 显式测试库参数优先，不允许继承生产连接串。
env.pop('ConnectionStrings__EcommerceLab', None)
password = secrets.token_urlsafe(24)
process = None
created = False
checks = 0

def request(path, method='GET', body=None, token=None, expected=200, headers=None):
    global checks
    h = {'Content-Type': 'application/json', **(headers or {})}
    if token: h['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(base + path, data=None if body is None else body if isinstance(body, bytes) else json.dumps(body).encode(), headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as res: status, raw = res.status, res.read()
    except urllib.error.HTTPError as e: status, raw = e.code, e.read()
    assert status in (expected if isinstance(expected, tuple) else (expected,)), (method, path, status, raw.decode()[:600])
    checks += 1
    payload = json.loads(raw)
    if path.startswith('/openapi/'):
        return payload
    assert set(payload) == {'success', 'code', 'msg', 'data'}, (path, payload)
    assert payload['success'] is (status < 400) and payload['code'] == status, (path, payload)
    assert isinstance(payload['msg'], str) and payload['msg'], (path, payload)
    if status >= 400:
        assert payload['data'] is None, (path, payload)
        return payload
    data = payload['data']
    if isinstance(data, dict) and 'records' in data:
        assert set(data) == {'countId', 'current', 'maxLimit', 'optimizeCountSql', 'orders', 'pages', 'records', 'searchCount', 'size', 'total'}, data
        assert data['pages'] == (data['total'] + data['size'] - 1) // data['size'], data
        assert data['maxLimit'] == 100 and len(data['records']) <= data['size'] <= data['maxLimit'], data
    return data

def post(path, body=None, token=None, expected=200, headers=None):
    return request(path, 'POST', body, token, expected, headers)

try:
    subprocess.run([dotnet(), 'build', '--no-restore'], cwd=ROOT, env=env, check=True, stdout=subprocess.DEVNULL)
    with psycopg.connect(dbname='postgres', host=env['PGHOST'], autocommit=True) as conn:
        conn.execute(sql.SQL('CREATE DATABASE {}').format(sql.Identifier(name)))
    created = True
    with psycopg.connect(dbname=name, host=env['PGHOST']) as conn:
        for file in sorted((ROOT/'db').glob('0[1-7]_*.sql')): conn.execute(file.read_text())
        conn.execute((ROOT/'db/11_api.sql').read_text())
        conn.execute("INSERT INTO catalog.brands(name,slug,country_code) VALUES('検証工房','check-brand','JP')")
        conn.execute("INSERT INTO catalog.categories(name,slug) VALUES('Home','home'),('Food','food')")
        conn.execute("INSERT INTO inventory.warehouses(code,name,postal_code,prefecture,city,address) VALUES('QA-TOKYO','検証東京倉庫','100-0001','東京都','千代田区','千代田1-1')")
        conn.execute((ROOT/'db/10_comments.sql').read_text())
    dll = ROOT/'bin/Debug/net10.0/mini-store.dll'
    for role in ('operator', 'viewer'):
        subprocess.run([dotnet(), str(dll), '--create-admin'], cwd=ROOT, env={**env, 'ADMIN_EMAIL': role+'@qa.example', 'ADMIN_PASSWORD': password, 'ADMIN_ROLE': role}, stdout=subprocess.DEVNULL, check=True)
    logdir=ROOT/'.local'; logdir.mkdir(exist_ok=True)
    with (logdir/'api-test.log').open('w') as log:
        process = subprocess.Popen([dotnet(), str(dll)], cwd=ROOT, env=env, stdout=log, stderr=log)
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError('测试服务器提前退出，检查 .local/api-test.log')
            try: request('/health'); break
            except (urllib.error.URLError, ConnectionError): time.sleep(.2)
        else: raise RuntimeError('测试服务器启动失败，检查 .local/api-test.log')
        operator = post('/api/admin/auth/login', {'email':'operator@qa.example','password':password})['accessToken']
        viewer = post('/api/admin/auth/login', {'email':'viewer@qa.example','password':password})['accessToken']
        customers = [post('/api/auth/register', {'email':f'customer{i}@qa.example','password':password,'firstName':'美咲','lastName':'検証'}) for i in range(2)]
        token = customers[0]['accessToken']; stranger = customers[1]['accessToken']
        request('/api/admin/orders', expected=401)
        request('/api/admin/orders', token=token, expected=403)
        request('/api/store/products?pageSize=0', expected=400)
        post('/api/auth/login', b'{bad json', expected=400)
        request('/api/store/products?pageSize=no-number', expected=400)
        request('/api/store/products?pageSize=101', expected=400)
        request('/api/unknown-resource', expected=404)
        request('/api/config', 'DELETE', expected=405)
        empty = request('/api/store/products?q=does-not-exist')
        assert empty['records'] == [] and empty['pages'] == 0 and empty['current'] == 1
        # 下划线必须按字面量搜索；未转义时 ILIKE 会把它当成单字符通配符并命中全部商品。
        assert request('/api/store/products?q=_')['records'] == []
        brands=request('/api/admin/brands',token=operator); cats=request('/api/admin/categories',token=operator)
        product={'name':'検証用・日常のカップ','slug':'qa-cup','description':'API 真实闭环测试商品','brandId':brands[0]['id'],'status':'active','categoryIds':[cats[0]['id']], 'variants':[{'id':None,'sku':'QA-CUP','name':'白','price':1000,'isActive':True,'weightGrams':250,'attributes':{'color':'white'}}]}
        post('/api/admin/products',product,viewer,403)
        p=post('/api/admin/products',product,operator,201); pid=p['id']; variant=p['variants'][0]['id']
        request('/api/admin/products/'+pid+'/status','PATCH',{'status':'active'},operator,400)
        request('/api/admin/products/'+pid+'/status','PATCH',{'status':'active','version':p['version']},operator,200)
        request('/api/admin/products/'+pid+'/status','PATCH',{'status':'inactive','version':p['version']},operator,409)
        warehouse=request('/api/admin/inventory/warehouses',token=operator)[0]['id']
        stockpath=f'/api/admin/inventory/stocks/{warehouse}/{variant}/adjust'
        post(stockpath,{'quantity':10,'reason':'隔离测试进货'},operator,200)
        address={'addressType':'shipping','recipientName':'検証 美咲','postalCode':'100-0001','countryCode':'JP','prefecture':'東京都','city':'千代田区','addressLine1':'千代田1-1','addressLine2':None,'phone':'09012345678','isDefault':True}
        addr=post('/api/me/addresses',address,token)
        request('/api/me/addresses/'+addr['id'],'DELETE',token=stranger,expected=404)
        for who in [token,stranger]: request('/api/me/cart/items/'+variant,'PUT',{'quantity':2},who,200)
        # 同一用户多地址模型、原子默认地址切换。
        addr2=post('/api/me/addresses',{**address,'addressLine1':'丸の内2-2'},token)
        assert sum(a['isDefault'] for a in request('/api/me/addresses',token=token))==1
        now=datetime.now(timezone.utc)
        coupon={'code':'QA10','name':'検証优惠','discountType':'percentage','discountValue':10,'minOrderAmount':0,'maxDiscountAmount':500,'usageLimit':10,'startsAt':(now-timedelta(days=1)).isoformat(),'endsAt':(now+timedelta(days=1)).isoformat(),'isActive':True}
        coupon_id = post('/api/admin/coupons',coupon,operator)['id']
        request('/api/admin/coupons/'+coupon_id+'/status','PATCH', {}, operator, 400)
        quote_body={'addressId':addr2['id'],'shippingMethod':'standard','couponCode':'QA10'}
        q=post('/api/me/checkout/quote',quote_body,token)
        assert q['totals']=={'subtotal':2000,'discountTotal':200,'taxTotal':180,'shippingTotal':500,'grandTotal':2480,'currency':'JPY'},q
        body={**quote_body,'quoteToken':q['quoteToken']}; key=str(uuid.uuid4())
        def place(_):return post('/api/me/orders',body,token,(200,201),{'Idempotency-Key':key})
        with ThreadPoolExecutor(2) as pool: orders=list(pool.map(place,range(2)))
        assert orders[0]['id']==orders[1]['id']; oid=orders[0]['id']
        post('/api/me/orders',{**body,'shippingMethod':'express'},token,409,{'Idempotency-Key':key})
        request('/api/me/orders/'+oid,token=stranger,expected=404)
        detail=request('/api/admin/orders/'+oid,token=operator)
        assert detail['fulfillmentWarehouseId']==warehouse
        assert request('/api/admin/orders',token=operator)['total']==1
        post(stockpath,{'quantity':-9,'reason':'拒绝扣掉预占库存'},operator,409)
        post('/api/me/orders/'+oid+'/simulate-payment', {}, token, 400)
        post('/api/me/orders/'+oid+'/simulate-payment',{'success':False},token)
        post('/api/me/orders/'+oid+'/simulate-payment',{'success':True},token)
        post('/api/me/orders/'+oid+'/simulate-payment',{'success':True},token)
        post('/api/admin/orders/'+oid+'/process',token=operator,expected=200)
        post('/api/admin/orders/'+oid+'/ship',{'warehouseId':warehouse,'carrier':'Yamato','trackingNumber':'QA123456789'},operator)
        post('/api/admin/orders/'+oid+'/deliver',token=operator,expected=200)
        detail=request('/api/me/orders/'+oid,token=token)
        assert detail['status']=='delivered'
        item=detail['items'][0]['id']
        rev=post(f'/api/me/orders/{oid}/items/{item}/review',{'rating':5,'title':'好用','content':'已收到商品。'},token)
        post('/api/admin/reviews/'+rev['id']+'/review',{'status':'published'},operator,200)
        assert request(f'/api/store/products/{pid}/reviews')['total']==1
        payment=next(p for p in detail['payments'] if p['status']=='captured')
        def refund(_):return post('/api/admin/refunds',{'paymentId':payment['id'],'amount':2000,'reason':'并发超额保护'},operator,(200,409))
        with ThreadPoolExecutor(2) as pool: refunds=list(pool.map(refund,range(2)))
        succeeded=[r for r in refunds if 'id' in r];assert len(succeeded)==1
        post('/api/admin/refunds/'+succeeded[0]['id']+'/review', {}, operator, 400)
        post('/api/admin/refunds/'+succeeded[0]['id']+'/review',{'approved':True},operator,200)
        # 再次结算 -> 取消，验证已转换购物车不会被当成唯一一对一导航。
        request('/api/me/cart/items/'+variant,'PUT',{'quantity':1},token,200)
        q=post('/api/me/checkout/quote',quote_body,token)
        cancelled=post('/api/me/orders',{**quote_body,'quoteToken':q['quoteToken']},token,201,{'Idempotency-Key':str(uuid.uuid4())})
        post('/api/me/orders/'+cancelled['id']+'/cancel',token=token,expected=200)
        for resource in ['products','orders','customers','payments','refunds','shipments','coupons','reviews','inventory/stocks','inventory/stock-movements']:
            result=request('/api/admin/'+resource+'?pageSize=2',token=viewer);assert isinstance(result['records'],list)
        request('/api/admin/dashboard',token=operator)
        # 重构回归：真实 SQL 排序、具名响应 DTO 与框架 JSON 绑定必须保持可用。
        food = {**product, 'name':'検証・食品', 'slug':'qa-food', 'categoryIds':[next(c['id'] for c in cats if c['name']=='Food')], 'variants':[{**product['variants'][0], 'sku':'QA-FOOD', 'price':1001}]}
        food_id = post('/api/admin/products',food,operator,201)['id']
        # 税前 1001 的食品含税 1081，低于税前 1000 的杯子含税 1100。
        for sort, first in [('price_asc', food_id), ('price_desc', pid), ('rating', pid), ('newest', food_id)]:
            listed = request('/api/store/products?sort='+sort)
            assert listed['records'][0]['id'] == first

        assert request('/api/store/products/'+p['slug'])['variants'][0]['id'] == variant
        assert request('/api/store/products/by-id/'+pid)['id'] == pid
        assert request('/api/me',token=token)['id'] == customers[0]['userId']
        assert request('/api/admin/customers/'+customers[0]['userId'],token=operator)['addresses']
        request('/api/me/cart/items/'+variant,'PUT',{'quantity':1},token,200)
        cart = request('/api/me/cart',token=token)
        assert cart['items'][0]['variantId'] == variant and cart['totals']['grandTotal'] == 1600
        request('/api/me/cart/items/'+variant,'DELETE',token=token,expected=200)
        schema = request('/openapi/v1.json')
        for dto in ['ProductDetailDto','OrderDetailDto','CartDto','DashboardDto','PaymentSimulationDto']:
            assert dto in schema['components']['schemas'], dto

        # 文档也是对外契约：每个路由都有中文说明、稳定名称、实际授权和响应模型。
        operation_ids = set()
        for path, methods in schema['paths'].items():
            for method, operation in methods.items():
                if method not in ('get', 'post', 'put', 'patch', 'delete'):
                    continue
                assert operation.get('summary') and operation.get('description') and operation.get('tags'), (method, path)
                operation_id = operation['operationId']
                assert operation_id not in operation_ids, operation_id
                operation_ids.add(operation_id)
                protected = path.startswith('/api/me') or (path.startswith('/api/admin') and path != '/api/admin/auth/login')
                assert bool(operation.get('security')) == protected, path
                if protected:
                    assert {'401', '403'} <= operation['responses'].keys(), path
                for parameter in operation.get('parameters', []):
                    assert parameter.get('description'), (path, parameter['name'])
                for status, response in operation['responses'].items():
                    assert response.get('description'), (path, status)
                    response_schema = response.get('content', {}).get('application/json', {}).get('schema', {})
                    ref = response_schema.get('$ref', '')
                    assert 'ApiResponse' in ref, (path, status, response_schema)
                    envelope = schema['components']['schemas'][ref.rsplit('/', 1)[1]]
                    assert set(envelope['properties']) == {'success', 'code', 'msg', 'data'}, ref
                for media in operation.get('requestBody', {}).get('content', {}).values():
                    ref = media.get('schema', {}).get('$ref')
                    if ref:
                        dto = schema['components']['schemas'][ref.rsplit('/', 1)[1]]
                        assert all(p.get('description') for p in dto.get('properties', {}).values()), ref
        assert schema['components']['securitySchemes']['Bearer']['scheme'] == 'bearer'
        place = schema['paths']['/api/me/orders']['post']
        assert {'200', '201', '409', '422'} <= place['responses'].keys()
        assert any(p['name'] == 'Idempotency-Key' and p['in'] == 'header' and p['required'] for p in place['parameters'])
        assert all(p.get('description') for p in schema['components']['schemas']['VariantRequest']['properties'].values())
        # Scalar 使用相同文档，页面和脚本均由本地服务提供。
        with urllib.request.urlopen(base + '/scalar/') as res:
            html = res.read()
            assert res.status == 200 and b'openapi/v1.json' in html
            assert b'"targetKey":"csharp"' in html
        for asset in ('scalar.js', 'scalar.aspnetcore.js'):
            with urllib.request.urlopen(base + '/scalar/' + asset) as res:
                assert res.status == 200 and res.read(), asset

        # 在最后触发限流，以免影响前面的登录；限流也必须返回公司响应结构。
        for _ in range(21):
            post('/api/auth/login', {'email':'missing@qa.example','password':password}, expected=(401,429))
        post('/api/auth/login', {'email':'missing@qa.example','password':password}, expected=429)
        (ROOT/'docs/backend/openapi.json').write_text(json.dumps(schema, ensure_ascii=False, indent=2) + '\n')

        # 通用 SQL 审计包含订单合计、退款、库存流水、所有外键和时间线。
        with psycopg.connect(dbname=name,host=env['PGHOST']) as conn:
            conn.execute((ROOT/'db/08_verify.sql').read_text())
            assert conn.execute('SELECT coalesce(sum(violations),0) FROM lab_checks').fetchone()[0]==0
        print(f'PASS: {checks} HTTP checks; checkout idempotency, authorization, concurrency, fulfillment, refund, reviews and SQL audit.')
finally:
    if process:
        process.terminate()
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired: process.kill();process.wait()
    if created:
        with psycopg.connect(dbname='postgres',host=env['PGHOST'],autocommit=True) as conn:
            conn.execute(sql.SQL('DROP DATABASE {} WITH (FORCE)').format(sql.Identifier(name)))
