$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$python = if (Test-Path '.venv/Scripts/python.exe') { '.venv/Scripts/python.exe' } else { '.venv/bin/python' }
& $python scripts/manage_db.py reset
if ($LASTEXITCODE -ne 0) { throw 'Database reset failed' }
