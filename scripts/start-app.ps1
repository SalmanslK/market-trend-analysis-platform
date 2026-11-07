# Starts PostgreSQL, the API, the Worker and the website in the background, then opens the app.
# Safe to run again: anything already running is left alone. Logs go to the logs folder.
# Usage: double-click start-app.cmd in the repo root, or run this script.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root 'logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Test-Port([int]$Port) {
    [bool](Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
}

function Wait-ForPort([int]$Port, [string]$Name, [string]$Log, [int]$TimeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Port $Port) {
            Write-Host "  $Name is ready on port $Port" -ForegroundColor Green
            return
        }
        Start-Sleep -Seconds 1
    }
    throw "$Name did not start within $TimeoutSeconds seconds. See $Log"
}

function Start-Hidden([string]$Name, [string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory) {
    $log = Join-Path $logs "$Name.log"
    Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -WindowStyle Hidden `
        -RedirectStandardOutput $log -RedirectStandardError (Join-Path $logs "$Name.err.log") | Out-Null
    return $log
}

Write-Host 'Starting Market Analytics...'

# PostgreSQL: use whatever already listens on 5432, otherwise the portable install from SETUP_CHECKLIST.md
if (Test-Port 5432) {
    Write-Host '  PostgreSQL is already running'
} else {
    $pgsql = Join-Path $env:LOCALAPPDATA 'Programs\postgresql-17\pgsql'
    $pgCtl = Join-Path $pgsql 'bin\pg_ctl.exe'
    if (-not (Test-Path $pgCtl)) {
        throw "PostgreSQL is not running and was not found at $pgsql. See SETUP_CHECKLIST.md."
    }
    $pgLog = Join-Path $pgsql 'postgres.log'
    Start-Process -FilePath $pgCtl -ArgumentList @('-D', "`"$(Join-Path $pgsql 'data')`"", '-l', "`"$pgLog`"", 'start') -WindowStyle Hidden | Out-Null
    Wait-ForPort 5432 'PostgreSQL' $pgLog 60
}

# One backup a day of prices and the collected news, which would take hours to download again.
# A failed backup is reported but doesn't stop the app
try {
    & (Join-Path $PSScriptRoot 'backup-db.ps1') -IfOlderThanHours 20
} catch {
    Write-Host "  Backup failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

# The API and Worker read the database password from user secrets, shared by both
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5000'

$apiRunning = Test-Port 5000
$workerRunning = [bool](Get-Process -Name 'MarketAnalytics.Worker' -ErrorAction SilentlyContinue)

# Build first and start with --no-build, so the API and Worker don't compete for the same build output.
# Only possible while neither is running, because they lock their DLLs.
if (-not $apiRunning -and -not $workerRunning) {
    Write-Host '  Building the backend...'
    & dotnet build (Join-Path $root 'backend\MarketAnalytics.sln') -nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The backend failed to build. Run: dotnet build backend\MarketAnalytics.sln' }
}

if ($apiRunning) {
    Write-Host '  API is already running'
} else {
    $log = Start-Hidden 'api' 'dotnet' @('run', '--no-build', '--no-launch-profile') (Join-Path $root 'backend\MarketAnalytics.Api')
    Wait-ForPort 5000 'API' $log
}

if ($workerRunning) {
    Write-Host '  Worker is already running'
} else {
    Start-Hidden 'worker' 'dotnet' @('run', '--no-build', '--no-launch-profile') (Join-Path $root 'backend\MarketAnalytics.Worker') | Out-Null
    Write-Host '  Worker started (checks prices every 6 hours, news hourly)' -ForegroundColor Green
}

if (Test-Port 3000) {
    Write-Host '  Website is already running'
} else {
    $frontend = Join-Path $root 'frontend'
    if (-not (Test-Path (Join-Path $frontend 'node_modules'))) {
        Write-Host '  Installing website dependencies...'
        Push-Location $frontend
        try { & npm.cmd install --no-fund --no-audit | Out-Null } finally { Pop-Location }
    }
    $log = Start-Hidden 'website' 'cmd.exe' @('/c', 'npm', 'run', 'dev') $frontend
    Wait-ForPort 3000 'Website' $log
}

Write-Host ''
Write-Host 'Market Analytics is running at http://localhost:3000' -ForegroundColor Green
Write-Host 'To stop it, run stop-app.cmd. It keeps running after you close this window.'

if (-not $env:MARKET_ANALYTICS_NO_BROWSER) {
    Start-Process 'http://localhost:3000'
}
