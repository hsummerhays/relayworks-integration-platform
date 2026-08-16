param(
    [switch]$Build,
    [switch]$Down,
    [switch]$WithPhp = $true,
    [switch]$WithoutPhp,
    [switch]$SkipMigrations,
    [switch]$NoApps
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

$ComposeServiceBus = Join-Path $RepoRoot ".compose\service-bus\docker-compose.yml"
$ComposePhp = Join-Path $RepoRoot "web\relayworks-portal-php\compose.yaml"
$StopScript = Join-Path $ScriptDir "stop-local-dev.ps1"

if ($Down) {
    $stopArgs = @()
    if ($WithoutPhp -or -not $WithPhp) {
        $stopArgs += "-WithoutPhp"
    }
    & $StopScript @stopArgs
    exit $LASTEXITCODE
}

Write-Host "=== Starting RelayWorks Local Development Stack ===" -ForegroundColor Cyan

# 1. Start Service Bus & MSSQL emulator stack
if (Test-Path $ComposeServiceBus) {
    Write-Host "Starting Azure Service Bus Emulator & SQL Server containers..." -ForegroundColor Cyan
    if (-not $env:MSSQL_SA_PASSWORD) {
        $env:MSSQL_SA_PASSWORD = "Password123!"
    }
    docker compose -f $ComposeServiceBus up -d
}

# 2. Start PHP Portal container (enabled by default)
$shouldStartPhp = $WithPhp -and -not $WithoutPhp
if ($shouldStartPhp -and (Test-Path $ComposePhp)) {
    Write-Host "Starting RelayWorks PHP Portal in Docker..." -ForegroundColor Cyan
    $phpArgs = @("compose", "-f", $ComposePhp, "up", "-d")
    if ($Build) {
        $phpArgs += "--build"
    }
    & docker $phpArgs
    Write-Host "PHP Portal running at http://localhost:8080" -ForegroundColor Green
}

# 3. Wait for SQL Server readiness
Write-Host "Waiting for SQL Server to become available..." -ForegroundColor Cyan
$maxRetries = 30
$retryCount = 0
$sqlReady = $false

while (-not $sqlReady -and $retryCount -lt $maxRetries) {
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $asyncResult = $tcpClient.BeginConnect("127.0.0.1", 1433, $null, $null)
        $success = $asyncResult.AsyncWaitHandle.WaitOne(1000, $false)
        if ($success -and $tcpClient.Connected) {
            $tcpClient.EndConnect($asyncResult)
            $tcpClient.Close()
            $sqlReady = $true
            break
        }
        $tcpClient.Close()
    } catch {
        # ignore and retry
    }
    $retryCount++
    Start-Sleep -Seconds 1
}

if ($sqlReady) {
    Write-Host "SQL Server is listening on 127.0.0.1:1433" -ForegroundColor Green
    Start-Sleep -Seconds 2
} else {
    Write-Warning "SQL Server did not respond within timeout, proceeding anyway..."
}

# 4. Run EF Core / Schema Migrations
if (-not $SkipMigrations) {
    $MigrationScript = Join-Path $ScriptDir "run-migrations.ps1"
    if (Test-Path $MigrationScript) {
        Write-Host "Running Database Migrations..." -ForegroundColor Cyan
        & $MigrationScript
    }
}

# 5. Launch Backend Control Plane API & Vue Console if requested
if (-not $NoApps) {
    Write-Host "Starting Control Plane API in a separate terminal window..." -ForegroundColor Cyan
    $apiProjectPath = Join-Path $RepoRoot "src\RelayWorks.ControlPlane.Api\RelayWorks.ControlPlane.Api.csproj"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Starting Control Plane API on http://localhost:5080...' -ForegroundColor Cyan; dotnet run --project `"$apiProjectPath`""

    Write-Host "Starting Vue Console frontend in a separate terminal window..." -ForegroundColor Cyan
    $consoleDir = Join-Path $RepoRoot "web\relayworks-console"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location `"$consoleDir`"; Write-Host 'Starting Vue Console Dev Server...' -ForegroundColor Cyan; npm run dev"
}

Write-Host "=== RelayWorks Local Environment is Ready! ===" -ForegroundColor Green
Write-Host " - Control Plane API : http://localhost:5080" -ForegroundColor Yellow
Write-Host " - Vue Console UI    : http://localhost:5173" -ForegroundColor Yellow
if ($shouldStartPhp) {
    Write-Host " - PHP Portal UI     : http://localhost:8080" -ForegroundColor Yellow
}
