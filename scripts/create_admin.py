"""交互创建管理员，不把明文密码放进命令历史或源码。"""
import getpass
import os
import subprocess
from run_api import ROOT, dotnet

if __name__ == '__main__':
    email = input('后台邮箱：').strip()
    role = input('角色 operator / viewer [operator]：').strip() or 'operator'
    password = getpass.getpass('密码（至少 12 字符）：')
    if password != getpass.getpass('再次输入密码：'):
        raise SystemExit('两次密码不一致，未创建账号。')
    subprocess.run([dotnet(), 'run', '--no-launch-profile', '--', '--create-admin'], cwd=ROOT,
                   env={**os.environ, 'ADMIN_EMAIL': email, 'ADMIN_ROLE': role, 'ADMIN_PASSWORD': password}, check=True)
