$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$PhpDir = Join-Path $RepoRoot "web\relayworks-portal-php"

Write-Host "Running PHP Portal Test Suite..." -ForegroundColor Cyan

# Check if php is installed locally
$phpCmd = Get-Command php -ErrorAction SilentlyContinue

if ($phpCmd) {
    & php (Join-Path $PhpDir "tests\run-tests.php")
} else {
    # Fallback to running in docker container
    $containerRunning = docker ps -q -f name=relayworks-portal-php
    if (-not $containerRunning) {
        Write-Host "Starting temporary container to run tests..." -ForegroundColor Yellow
        docker run --rm -v "${PhpDir}:/app" -w /app php:8.3-cli-alpine php tests/run-tests.php
    } else {
        docker exec relayworks-portal-php php tests/run-tests.php
    }
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "All PHP tests passed!" -ForegroundColor Green
} else {
    Write-Error "PHP test suite failed with code $LASTEXITCODE"
}
