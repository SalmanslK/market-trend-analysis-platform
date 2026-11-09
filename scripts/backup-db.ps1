# Backs up the MarketAnalytics database (prices, news, payouts) to the backups folder and keeps the newest 14.
# Usage: powershell -File scripts\backup-db.ps1 [-IfOlderThanHours 20]
# start-app runs it with -IfOlderThanHours 20, so there is at most one backup a day.
# Restore: pg_restore -h localhost -U postgres -d MarketAnalytics --clean <file>.dump

param([int]$IfOlderThanHours = 0, [int]$Keep = 14)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$backups = Join-Path $root 'backups'
New-Item -ItemType Directory -Force $backups | Out-Null

$latest = Get-ChildItem $backups -Filter 'MarketAnalytics-*.dump' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($IfOlderThanHours -gt 0 -and $latest -and $latest.LastWriteTime -gt (Get-Date).AddHours(-$IfOlderThanHours)) {
    Write-Host "  Backup skipped: the latest is from $($latest.LastWriteTime.ToString('g'))"
    return
}

# pg_dump from the portable install, or from PATH
$pgDump = Join-Path $env:LOCALAPPDATA 'Programs\postgresql-17\pgsql\bin\pg_dump.exe'
if (-not (Test-Path $pgDump)) {
    $pgDump = (Get-Command pg_dump -ErrorAction SilentlyContinue).Source
    if (-not $pgDump) { throw 'pg_dump was not found. See SETUP_CHECKLIST.md.' }
}

# The connection string lives in the API's user secrets; it is read here and never printed
$secrets = & dotnet user-secrets list --project (Join-Path $root 'backend\MarketAnalytics.Api') 2>$null
$connection = ($secrets | Where-Object { $_ -like 'ConnectionStrings:DefaultConnection = *' }) -replace '^ConnectionStrings:DefaultConnection = ', ''
if (-not $connection) { throw 'No ConnectionStrings:DefaultConnection in the API user secrets. See SETUP_CHECKLIST.md.' }
$parts = @{}
foreach ($pair in $connection.Split(';')) {
    $kv = $pair.Split('=', 2)
    if ($kv.Count -eq 2) { $parts[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim() }
}

$file = Join-Path $backups ("MarketAnalytics-{0}.dump" -f (Get-Date -Format 'yyyy-MM-dd-HHmm'))
$env:PGPASSWORD = $parts['password']
try {
    $hostName = if ($parts['host']) { $parts['host'] } else { 'localhost' }
    $port = if ($parts['port']) { $parts['port'] } else { '5432' }
    $user = if ($parts['username']) { $parts['username'] } else { 'postgres' }
    $database = if ($parts['database']) { $parts['database'] } else { 'MarketAnalytics' }
    & $pgDump -h $hostName -p $port -U $user -d $database -F c -f $file
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit code $LASTEXITCODE" }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

$sizeMb = [math]::Round((Get-Item $file).Length / 1MB, 1)
Write-Host "  Backed up the database to backups\$(Split-Path -Leaf $file) ($sizeMb MB)" -ForegroundColor Green

Get-ChildItem $backups -Filter 'MarketAnalytics-*.dump' | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Keep | Remove-Item
