# ============================================================
# ClassView IIS Deploy Script
# ============================================================

# IIS 管理模块依赖 Windows PowerShell 5.1（Desktop）
if ($PSVersionTable.PSEdition -ne "Desktop") {
    Write-Host "检测到当前是 PowerShell 7，正在切换到 Windows PowerShell 5.1 继续执行..."
    $scriptPath = $MyInvocation.MyCommand.Path
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $scriptPath
    exit $LASTEXITCODE
}

# ---------- ERP SQL Server 连接配置 ----------
$SQL_HOST = "192.168.10.26,1433"
$SQL_DATABASE = "CJT800"
$SQL_USERNAME = "sa"
$SQL_PASSWORD = "Sa20251224@.com1357"

# ---------- ClassView 元数据库（SqlList 独立 SQL Server）连接配置 ----------
$META_SQL_HOST = "192.168.10.26,1433"
$META_SQL_DATABASE = "SqlList"
$META_SQL_USERNAME = "sa"
$META_SQL_PASSWORD = "Sa20251224@.com1357"

# ---------- IIS 后端端口 ----------
$BACKEND_PORT = 8999

# ---------- 其他部署参数 ----------
$SITE_NAME = "ClassView"
$APP_POOL = "ClassViewPool"
$PROJECT_ROOT = "D:\classview"
$WEB_CONFIG_PATH = Join-Path $PROJECT_ROOT "Web.Config"
$SQLSERVER_INIT_SQL = Join-Path $PROJECT_ROOT "deploy\sqlserver\init_classview_meta.sql"
$PACKAGES_CONFIG = Join-Path $PROJECT_ROOT "packages.config"
$PACKAGES_DIR = Join-Path $PROJECT_ROOT "packages"
$BIN_DIR = Join-Path $PROJECT_ROOT "Bin"

Import-Module WebAdministration -ErrorAction Stop

function Ensure-IISDrive {
    if (-not (Get-PSDrive -Name IIS -ErrorAction SilentlyContinue)) {
        New-PSDrive -Name IIS -PSProvider WebAdministration -Root "\" | Out-Null
    }
}

function Update-WebConfigConnectionStrings {
    param(
        [string]$ConfigPath
    )

    [xml]$xml = Get-Content -LiteralPath $ConfigPath

    $erpConn = "User Id=$SQL_USERNAME;Password=$SQL_PASSWORD;Data Source=$SQL_HOST;Initial Catalog=$SQL_DATABASE;packet size=4096;Max Pool size=1500;persist security info=True;Encrypt=True;TrustServerCertificate=True"
    $metaConn = "User Id=$META_SQL_USERNAME;Password=$META_SQL_PASSWORD;Data Source=$META_SQL_HOST;Initial Catalog=$META_SQL_DATABASE;packet size=4096;Max Pool size=1500;persist security info=True;Encrypt=True;TrustServerCertificate=True"

    $connNodes = $xml.configuration.connectionStrings.add
    foreach ($node in $connNodes) {
        if ($node.name -eq "ConnectionString") {
            $node.connectionString = $erpConn
        }
        if ($node.name -eq "MetaConnection") {
            $node.connectionString = $metaConn
            $node.providerName = "System.Data.SqlClient"
        }
    }

    $xml.Save($ConfigPath)
    Write-Host "Web.Config connectionStrings 更新完成"
}

function Resolve-NuGetExe {
    $nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
    if ($nuget) {
        return $nuget.Source
    }

    $localNuget = Join-Path $PROJECT_ROOT "nuget.exe"
    if (-not (Test-Path $localNuget)) {
        Write-Host "下载 nuget.exe ..."
        Invoke-WebRequest -UseBasicParsing -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $localNuget
    }
    return $localNuget
}

function Get-PreferredLibDir {
    param(
        [string]$PackageLibRoot
    )

    $candidates = @("net48", "net472", "net471", "net47", "net462", "net461", "net46", "net45", "net40", "netstandard2.0")
    foreach ($tfm in $candidates) {
        $p = Join-Path $PackageLibRoot $tfm
        if (Test-Path $p) {
            return $p
        }
    }

    $any = Get-ChildItem -Path $PackageLibRoot -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($any) { return $any.FullName }
    return $null
}

function Ensure-BinDependencies {
    if (-not (Test-Path $PACKAGES_CONFIG)) {
        Write-Warning "未找到 packages.config，跳过 NuGet 依赖还原。"
        return
    }

    $nugetExe = Resolve-NuGetExe
    if (-not (Test-Path $PACKAGES_DIR)) {
        New-Item -ItemType Directory -Path $PACKAGES_DIR | Out-Null
    }

    Write-Host "还原 NuGet 包..."
    & $nugetExe install $PACKAGES_CONFIG -OutputDirectory $PACKAGES_DIR -NonInteractive
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet 依赖还原失败。"
    }

    [xml]$pkgXml = Get-Content -LiteralPath $PACKAGES_CONFIG
    foreach ($pkg in $pkgXml.packages.package) {
        $id = [string]$pkg.id
        $version = [string]$pkg.version
        $pkgPath = Join-Path $PACKAGES_DIR "$id.$version"
        $libRoot = Join-Path $pkgPath "lib"
        if (-not (Test-Path $libRoot)) { continue }

        $libDir = Get-PreferredLibDir -PackageLibRoot $libRoot
        if (-not $libDir) { continue }

        Get-ChildItem -Path $libDir -Filter *.dll -File -ErrorAction SilentlyContinue | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $BIN_DIR -Force
        }
    }

    Write-Host "Bin 依赖拷贝完成"
}

function Ensure-IISPoolAndSite {
    Ensure-IISDrive

    if (-not (Test-Path "IIS:\AppPools\$APP_POOL")) {
        New-WebAppPool -Name $APP_POOL -Force | Out-Null
    }

    Set-ItemProperty "IIS:\AppPools\$APP_POOL" -Name managedRuntimeVersion -Value "v4.0"
    Set-ItemProperty "IIS:\AppPools\$APP_POOL" -Name managedPipelineMode -Value "Integrated"

    if (-not (Test-Path "IIS:\Sites\$SITE_NAME")) {
        New-Website -Name $SITE_NAME -PhysicalPath $PROJECT_ROOT -Port $BACKEND_PORT -ApplicationPool $APP_POOL -Force | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$SITE_NAME" -Name physicalPath -Value $PROJECT_ROOT
        Set-ItemProperty "IIS:\Sites\$SITE_NAME" -Name applicationPool -Value $APP_POOL

        $binding = "*:${BACKEND_PORT}:"
        $exists = Get-WebBinding -Name $SITE_NAME -Protocol "http" | Where-Object { $_.bindingInformation -eq $binding }
        if (-not $exists) {
            New-WebBinding -Name $SITE_NAME -Protocol "http" -Port $BACKEND_PORT -IPAddress "*" | Out-Null
        }
    }

    Write-Host "IIS 站点配置完成: http://localhost:$BACKEND_PORT/"
}

function Grant-IISPermission {
    $acl = Get-Acl $PROJECT_ROOT
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl -Path $PROJECT_ROOT -AclObject $acl
    Write-Host "IIS_IUSRS 读取权限已设置"

    $appDataPath = Join-Path $PROJECT_ROOT "App_Data"
    if (-not (Test-Path $appDataPath)) {
        New-Item -ItemType Directory -Path $appDataPath | Out-Null
    }

    $logsPath = Join-Path $appDataPath "Logs"
    if (-not (Test-Path $logsPath)) {
        New-Item -ItemType Directory -Path $logsPath | Out-Null
    }

    $logAcl = Get-Acl $appDataPath
    $logRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
    $logAcl.SetAccessRule($logRule)
    Set-Acl -Path $appDataPath -AclObject $logAcl
    Write-Host "IIS_IUSRS App_Data 写入权限已设置"
}

function New-SqlConnectionString {
    param(
        [string]$DatabaseName
    )

    return "User Id=$META_SQL_USERNAME;Password=$META_SQL_PASSWORD;Data Source=$META_SQL_HOST;Initial Catalog=$DatabaseName;packet size=4096;Max Pool size=1500;persist security info=True;Encrypt=True;TrustServerCertificate=True"
}

function Invoke-SqlNonQuery {
    param(
        [string]$ConnectionString,
        [string]$Sql
    )

    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 120
    $cmd.CommandText = $Sql

    try {
        $conn.Open()
        [void]$cmd.ExecuteNonQuery()
    }
    finally {
        $cmd.Dispose()
        $conn.Dispose()
    }
}

function Invoke-SqlScript {
    param(
        [string]$ConnectionString,
        [string]$ScriptPath
    )

    if (-not (Test-Path $ScriptPath)) {
        throw "未找到 SQL Server 初始化脚本: $ScriptPath"
    }

    $script = Get-Content -LiteralPath $ScriptPath -Raw
    $batches = [System.Text.RegularExpressions.Regex]::Split($script, "(?im)^\s*go\s*$")
    foreach ($batch in $batches) {
        if (-not [string]::IsNullOrWhiteSpace($batch)) {
            Invoke-SqlNonQuery -ConnectionString $ConnectionString -Sql $batch
        }
    }
}

function Init-SqlServerSchema {
    $escapedDbName = $META_SQL_DATABASE.Replace("]", "]]").Replace("'", "''")
    $masterConn = New-SqlConnectionString -DatabaseName "master"
    $metaConn = New-SqlConnectionString -DatabaseName $META_SQL_DATABASE

    $createDbSql = "if db_id(N'$escapedDbName') is null begin create database [$escapedDbName]; end"
    Invoke-SqlNonQuery -ConnectionString $masterConn -Sql $createDbSql
    Invoke-SqlScript -ConnectionString $metaConn -ScriptPath $SQLSERVER_INIT_SQL

    Write-Host "SqlList 元数据库初始化完成: $META_SQL_HOST/$META_SQL_DATABASE"
}

try {
    Update-WebConfigConnectionStrings -ConfigPath $WEB_CONFIG_PATH
    Ensure-BinDependencies
    Ensure-IISPoolAndSite
    Grant-IISPermission
    Init-SqlServerSchema
    iisreset | Out-Null

    Write-Host "==========================================="
    Write-Host "部署完成"
    Write-Host "后端地址: http://localhost:$BACKEND_PORT/"
    Write-Host "健康检查: http://localhost:$BACKEND_PORT/api/health/check"
    Write-Host "==========================================="
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
