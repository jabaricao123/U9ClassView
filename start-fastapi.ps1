$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendDir = Join-Path $scriptDir "backend-fastapi"
$requirements = Join-Path $backendDir "requirements.txt"
$port = 18743

if (-not (Test-Path $backendDir)) {
    throw "backend-fastapi 目录不存在: $backendDir"
}

if (-not (Test-Path $requirements)) {
    throw "requirements.txt 不存在: $requirements"
}

Set-Location $backendDir

python -m pip install -r $requirements
python -m uvicorn main:app --host 127.0.0.1 --port $port
