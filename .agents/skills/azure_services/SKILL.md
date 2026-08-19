---
name: azure_services
description: Start, stop, check status, or scale Azure Container Apps and serverless compute resources for RelayWorks.
---
# Azure Services Skill

Use this skill when asked to start, stop, pause, resume, or check status on RelayWorks cloud services in Azure (e.g. `rg-relayworks-dev`).

## Overview

RelayWorks uses Azure Container Apps for the Control Plane (`ca-relayworks-dev-control`) and Sync Worker (`ca-relayworks-dev-worker`), backed by Azure SQL Serverless (`sql-relayworks-dev`).

To minimize cloud spend without tearing down infrastructure:
- Deactivate Container App revisions (scales compute replicas down to 0).
- SQL Serverless automatically pauses when no incoming queries arrive (60 min idle threshold).

## Scripts

### Stop Azure Services
```powershell
./scripts/stop-azure-services.ps1 -ResourceGroup "rg-relayworks-dev"
```

### Start Azure Services
```powershell
./scripts/start-azure-services.ps1 -ResourceGroup "rg-relayworks-dev"
```

## Direct Azure CLI Commands

### Stop / Scale Replicas to 0
```powershell
# Deactivate active revision for control plane
az containerapp revision deactivate --name ca-relayworks-dev-control --resource-group rg-relayworks-dev --revision <revision-name>

# Deactivate active revision for sync worker
az containerapp revision deactivate --name ca-relayworks-dev-worker --resource-group rg-relayworks-dev --revision <revision-name>
```

### Start / Reactivate Replicas
```powershell
az containerapp revision activate --name ca-relayworks-dev-control --resource-group rg-relayworks-dev --revision <revision-name>
az containerapp revision activate --name ca-relayworks-dev-worker --resource-group rg-relayworks-dev --revision <revision-name>
```

### Check Status
```powershell
az containerapp revision list --name ca-relayworks-dev-control --resource-group rg-relayworks-dev --all -o table
az containerapp revision list --name ca-relayworks-dev-worker --resource-group rg-relayworks-dev --all -o table
az sql db list --server sql-relayworks-dev --resource-group rg-relayworks-dev --query "[].{name:name, status:status, sku:sku.name}" -o table
```
