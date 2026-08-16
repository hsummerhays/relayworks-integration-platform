$ErrorActionPreference = "Stop"

$csControl = "Server=127.0.0.1,1433;Database=relayworks-control;User Id=sa;Password=Password123!;TrustServerCertificate=True"
$csWorker = "Server=127.0.0.1,1433;Database=relayworks-worker;User Id=sa;Password=Password123!;TrustServerCertificate=True"

$env:ConnectionStrings__RelayWorks = $csControl
$env:ConnectionStrings__WorkerLedger = $csWorker

Write-Host "Executing RelayWorks Migrations on local SQL Server (127.0.0.1:1433)..." -ForegroundColor Cyan

dotnet run --project src/RelayWorks.Migrations/RelayWorks.Migrations.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host "Database migrations completed successfully!" -ForegroundColor Green
} else {
    Write-Error "Migration runner failed."
}
