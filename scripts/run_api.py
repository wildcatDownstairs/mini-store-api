"""本地开发启动器：密钥仅保存到已被 Git 忽略的 .local 中。"""
import os
from pathlib import Path
import secrets
import shutil

ROOT = Path(__file__).resolve().parents[1]

def environment():
    env = os.environ.copy()
    local = ROOT / '.local'
    local.mkdir(mode=0o700, exist_ok=True)
    key = local / 'signing-key'
    if not env.get('Auth__SigningKey'):
        if not key.exists():
            with key.open('x') as f:
                os.chmod(key, 0o600)
                f.write(secrets.token_urlsafe(48))
        env['Auth__SigningKey'] = key.read_text().strip()
    env.setdefault('ASPNETCORE_ENVIRONMENT', 'Development')
    env.setdefault('ASPNETCORE_URLS', 'http://127.0.0.1:5274')
    return env

def dotnet():
    return shutil.which('dotnet') or str(Path.home() / '.dotnet/dotnet')

if __name__ == '__main__':
    os.chdir(ROOT)
    os.execve(dotnet(), [dotnet(), 'run', '--no-launch-profile'], environment())
