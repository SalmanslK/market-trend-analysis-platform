# Stops the API, the Worker, the website and the portable PostgreSQL started by start-app.
# Usage: double-click stop-app.cmd in the repo root, or run this script.

$ErrorActionPreference = 'Continue'

Write-Host 'Stopping Market Analytics...'

foreach ($name in 'MarketAnalytics.Api', 'MarketAnalytics.Worker') {
    $processes = Get-Process -Name $name -ErrorAction SilentlyContinue
    if ($processes) {
        $processes | Stop-Process -Force
        Write-Host "  Stopped $name"
    }
}

# The website is whichever Node process serves port 3000; other Node programs are left alone
$website = Get-NetTCPConnection -State Listen -LocalPort 3000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($processId in $website) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($process -and $process.ProcessName -eq 'node') {
        Stop-Process -Id $processId -Force
        Write-Host '  Stopped the website'
    } elseif ($process) {
        Write-Host "  Port 3000 is used by $($process.ProcessName), which this script didn't start; left it running"
    }
}

# Only the portable PostgreSQL; a PostgreSQL installed as a Windows service is left alone
$pgsql = Join-Path $env:LOCALAPPDATA 'Programs\postgresql-17\pgsql'
$pgCtl = Join-Path $pgsql 'bin\pg_ctl.exe'
if (Test-Path $pgCtl) {
    & $pgCtl -D (Join-Path $pgsql 'data') status *> $null
    if ($LASTEXITCODE -eq 0) {
        & $pgCtl -D (Join-Path $pgsql 'data') stop -m fast *> $null
        Write-Host '  Stopped PostgreSQL'
    }
}

Write-Host 'Done.'
