"""Guarded local-only lab lifecycle; libpq reads PG* variables, never logs passwords."""
import os
from pathlib import Path
import re
import sys

import psycopg

ROOT = Path(__file__).resolve().parents[1]
DB = 'ecommerce_lab'
MARKER = 'mini_store:ecommerce_lab:v1:managed-learning-dataset'
TABLES = re.findall(r'CREATE TABLE ([a-z_]+\.[a-z_]+)', (ROOT/'db/03_tables.sql').read_text())
SCHEMAS = sorted({t.split('.')[0] for t in TABLES})


def connect(database=DB, **kwargs):
    if os.getenv('PGDATABASE', DB) != DB:
        raise RuntimeError('PGDATABASE must be ecommerce_lab; refusing another target')
    return psycopg.connect(dbname=database, **kwargs)


def assert_owned(conn):
    marker, owner = conn.execute("SELECT shobj_description(oid,'pg_database'), pg_get_userbyid(datdba)=current_user FROM pg_database WHERE datname=current_database()").fetchone()
    if marker != MARKER or not owner:
        raise RuntimeError('Database not identified as this task-owned lab; refusing mutation')


def assert_layout(conn, allow_api=False):
    actual = {f'{s}.{t}' for s,t in conn.execute("SELECT schemaname,tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')")}
    expected = set(TABLES)
    if allow_api and {'account.admin_users', 'sales.checkout_requests'}.issubset(actual):
        expected |= {'account.admin_users', 'sales.checkout_requests'}
    if actual != expected:
        raise RuntimeError(f'Unexpected or missing tables; refusing seed/reset: {actual ^ set(TABLES)}')


def main():
    action = sys.argv[1] if len(sys.argv)>1 else 'init'
    if action not in ('init','reset','comments'):
        raise SystemExit('Usage: manage_db.py init|reset|comments')
    if action == 'init':
        with connect('postgres', autocommit=True) as admin:
            # Serialize cooperating init processes, without modifying other databases.
            admin.execute('SELECT pg_advisory_lock(20260918,1)')
            exists = admin.execute('SELECT 1 FROM pg_database WHERE datname=%s',(DB,)).fetchone()
            if not exists:
                for statement in (ROOT/'db/00_create_database.sql').read_text().split(';'):
                    if statement.strip(): admin.execute(statement)
                print('Created ecommerce_lab', flush=True)
            else:
                print('ecommerce_lab exists; inspecting task marker; no DROP', flush=True)
            with connect() as conn:
                assert_owned(conn)
                present = conn.execute('SELECT nspname FROM pg_namespace WHERE nspname=ANY(%s)',(SCHEMAS,)).fetchall()
                if not present:
                    for path in sorted((ROOT/'db').glob('0[1-7]_*.sql')):
                        conn.execute(path.read_text())
                        print(f'{path.name} done', flush=True)
                else:
                    assert_layout(conn)
                    print('Existing schema retained', flush=True)
    with connect() as conn:
        assert_owned(conn)
        assert_layout(conn, allow_api=action == 'comments')
        conn.execute((ROOT/'db/10_comments.sql').read_text())
        print('Chinese table, view and column comments applied and verified', flush=True)
    if action == 'comments':
        return
    sys.path.insert(0,str(ROOT/'seed'))
    from seed import run
    run(reset=action=='reset')


if __name__ == '__main__':
    main()
