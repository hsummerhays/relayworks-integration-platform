param(
    [switch]$WithPhp = $true,
    [switch]$WithoutPhp,
    [switch]$OnlyPhp,
    [switch]$OnlyServiceBus
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

$ComposeServiceBus = Join-Path $RepoRoot ".compose\service-bus\docker-compose.yml"
$ComposePhp = Join-Path $RepoRoot "web\relayworks-portal-php\compose.yaml"

Write-Host "Stopping RelayWorks local environment containers..." -ForegroundColor Yellow

$stopServiceBus = $true
$stopPhp = $true

if ($OnlyPhp) {
    $stopServiceBus = $false
    $stopPhp = $true
} elseif ($OnlyServiceBus) {
    $stopServiceBus = $true
    $stopPhp = $false
} elseif ($WithoutPhp) {
    $stopPhp = $false
}

if ($stopServiceBus -and (Test-Path $ComposeServiceBus)) {
    Write-Host "Stopping Azure Service Bus Emulator & SQL Server containers..." -ForegroundColor Cyan
    docker compose -f $ComposeServiceBus down
}

if ($stopPhp -and (Test-Path $ComposePhp)) {
    Write-Host "Stopping RelayWorks PHP Portal container..." -ForegroundColor Cyan
    docker compose -f $ComposePhp down
}

Write-Host "Local environment stopped successfully." -ForegroundColor Green
