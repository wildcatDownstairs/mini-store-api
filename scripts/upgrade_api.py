"""只执行后端的增量 DDL；不会重置数据库或重新生成数据。"""
from manage_db import ROOT, connect, assert_owned

with connect() as conn:
    assert_owned(conn)
    conn.execute((ROOT / 'db/11_api.sql').read_text())
print('后端增量升级完成，原有数据已保留。')
