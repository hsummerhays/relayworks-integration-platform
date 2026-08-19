param(
    [string]$ResourceGroup = "rg-relayworks-dev"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Stopping RelayWorks Azure Compute Services ($ResourceGroup) ===" -ForegroundColor Cyan

# 1. Stop / Deactivate Container Apps (Control Plane and Sync Worker)
$apps = @("ca-relayworks-dev-control", "ca-relayworks-dev-worker")

foreach ($app in $apps) {
    Write-Host "Checking active revisions for $app..." -ForegroundColor Yellow
    $revisionsJson = az containerapp revision list --name $app --resource-group $ResourceGroup -o json 2>$null
    if ($LASTEXITCODE -eq 0 -and $revisionsJson) {
        $revisions = $revisionsJson | ConvertFrom-Json
        if ($revisions -and $revisions.Count -gt 0) {
            foreach ($rev in $revisions) {
                if ($rev.properties.active -eq $true) {
                    $revName = $rev.name
                    Write-Host "  Deactivating active revision: $revName..." -ForegroundColor Cyan
                    az containerapp revision deactivate --name $app --resource-group $ResourceGroup --revision $revName --output none
                    Write-Host "  Deactivated $revName." -ForegroundColor Green
                }
            }
        } else {
            Write-Host "  No active revisions running for $app." -ForegroundColor Gray
        }
    } else {
        Write-Warning "Could not retrieve revisions for $app or app does not exist."
    }
}

# 2. Azure SQL Databases status info
Write-Host "Azure SQL Serverless databases will auto-pause after idle duration (default 60m without active queries)." -ForegroundColor Yellow

Write-Host "=== Azure Services Stopped / Scaled to Zero Successfully ===" -ForegroundColor Green
