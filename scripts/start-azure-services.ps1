param(
    [string]$ResourceGroup = "rg-relayworks-dev"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Starting RelayWorks Azure Compute Services ($ResourceGroup) ===" -ForegroundColor Cyan

# 1. Activate latest revision for Container Apps (Control Plane and Sync Worker)
$apps = @("ca-relayworks-dev-control", "ca-relayworks-dev-worker")

foreach ($app in $apps) {
    Write-Host "Checking latest revisions for $app..." -ForegroundColor Yellow
    $revisionsJson = az containerapp revision list --name $app --resource-group $ResourceGroup --all -o json 2>$null
    if ($LASTEXITCODE -eq 0 -and $revisionsJson) {
        $revisions = $revisionsJson | ConvertFrom-Json
        if ($revisions -and $revisions.Count -gt 0) {
            # Sort by created time descending to get the most recent revision
            $latestRev = $revisions | Sort-Object { [DateTime]$_.properties.createdTime } -Descending | Select-Object -First 1
            $revName = $latestRev.name
            if ($latestRev.properties.active -ne $true) {
                Write-Host "  Activating revision $revName..." -ForegroundColor Cyan
                az containerapp revision activate --name $app --resource-group $ResourceGroup --revision $revName --output none
                Write-Host "  Activated $revName." -ForegroundColor Green
            } else {
                Write-Host "  Revision $revName is already active." -ForegroundColor Gray
            }
        } else {
            Write-Warning "No revisions found for $app."
        }
    } else {
        Write-Warning "Could not retrieve revisions for $app or app does not exist."
    }
}

Write-Host "=== Azure Services Started / Revisions Activated Successfully ===" -ForegroundColor Green
