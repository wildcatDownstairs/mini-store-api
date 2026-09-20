$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$python = if (Test-Path '.venv/Scripts/python.exe') { '.venv/Scripts/python.exe' } else { '.venv/bin/python' }
& $python scripts/manage_db.py init
if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed' }
