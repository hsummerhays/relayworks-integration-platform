param(
    [switch]$Build,
    [switch]$Down,
    [int]$Port = 8080
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$PhpDir = Join-Path $RepoRoot "web\relayworks-portal-php"

$StopScript = Join-Path $ScriptDir "stop-local-dev.ps1"

if ($Down) {
    if (Test-Path $StopScript) {
        & $StopScript -OnlyPhp
        exit $LASTEXITCODE
    } else {
        Write-Host "Stopping RelayWorks PHP Portal container..." -ForegroundColor Yellow
        docker compose -f (Join-Path $PhpDir "compose.yaml") down
        exit 0
    }
}

Write-Host "Starting RelayWorks PHP Portal in Docker on port $Port..." -ForegroundColor Cyan

$composeArgs = @("compose", "-f", (Join-Path $PhpDir "compose.yaml"), "up", "-d")
if ($Build) {
    $composeArgs += "--build"
}

& docker $composeArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "RelayWorks PHP Portal is running at http://localhost:$Port" -ForegroundColor Green
} else {
    Write-Error "Failed to start RelayWorks PHP Portal in Docker."
}
