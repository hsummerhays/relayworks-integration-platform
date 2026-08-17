# RelayWorks Integration Platform

RelayWorks is a .NET and Vue reference platform for reliable construction-system integrations. Iteration 19 establishes a formal provider adapter architecture with dynamic adapter registry and pluggable connector authentication strategies.

## Implemented vertical slice

1. An operator submits a tenant-scoped time-entry export through the Vue console.
2. The Control Plane saves the run and command outbox in one Azure SQL transaction.
3. Azure Service Bus delivers `IntegrationRunRequestedV1` at least once.
4. Before each destination call, the Worker acquires a unique record delivery gate in its own database.
5. The Worker resolves the target adapter via `IAdapterRegistry` and configures authentication (`IConnectorAuthenticator`).
6. The Worker persists record outcomes and versioned result events in one database transaction.
7. The Control Plane builds an operator-facing projection of rejected and ambiguous records.
8. `UnknownOutcome` records stop until a human verifies the destination and documents resolution.

The connectors are simulations. RelayWorks does not claim a production FieldFlo, Sage, QuickBooks, or other vendor connector.

## Safety invariant

> RelayWorks never retries a destination write whose outcome is unknown.

The ledger key is `(tenant, connection, operation, source record, source version)`. A canonical SHA-256 fingerprint detects changed data presented under the same source version. Neither Service Bus duplicate detection nor destination idempotency is treated as the primary financial-safety boundary.

## Service data ownership

| Service | Database | Owns |
| --- | --- | --- |
| Control Plane | `relayworks-control` | runs, command outbox, record projections, manual resolutions |
| Sync Worker | `relayworks-worker` | command inbox, processed-record ledger, event outbox |

Services share only versioned contracts and never query one another's database.

## Local Development Stack

RelayWorks includes local automation scripts for running the complete container and application ecosystem:

```powershell
# 1. Start the entire local environment (Containers, Migrations, API, and Vue UI)
.\scripts\start-local-dev.ps1

# 2. Stop and tear down all running local containers
.\scripts\stop-local-dev.ps1
# or
.\scripts\start-local-dev.ps1 -Down
```

### Local Services & Ports

| Service | Address | Description |
| --- | --- | --- |
| **Control Plane API** | `http://localhost:5080` | ASP.NET Core REST API & OpenAPI docs (`/openapi/v1.json`) |
| **Vue Operations Console** | `http://localhost:5173` | Vue 3 + Vite integration operations dashboard |
| **RelayWorks PHP Portal** | `http://localhost:8080` | Lightweight SSR PHP 8.3 reference web portal |
| **SQL Server 2022** | `127.0.0.1:1433` | Local database container (`relayworks-control`, `relayworks-worker`) |
| **Azure Service Bus Emulator**| `127.0.0.1:5300` / `5672` | AMQP emulator & HTTP administration endpoint |

### Script Options

- `.\scripts\start-local-dev.ps1 -WithoutPhp` : Start local environment without launching the PHP Portal container.
- `.\scripts\start-local-dev.ps1 -NoApps` : Start only background containers and execute database migrations.
- `.\scripts\start-php-portal.ps1` : Start or rebuild only the PHP portal container.
- `.\scripts\test-php-portal.ps1` : Run the zero-dependency PHP test suite locally or in Docker.

## Local validation

```bash
dotnet restore RelayWorks.slnx
dotnet build RelayWorks.slnx --configuration Release
dotnet test RelayWorks.slnx --configuration Release

cd web/relayworks-console
npm ci
npm run build

cd ../../infra/environments/dev
terraform init -backend=false
terraform validate
```

The slower Worker messaging test runs separately through `.github/workflows/service-bus-e2e.yml` because it requires the Azure Service Bus Emulator and SQL Server containers. It can also be run locally with Docker Compose from `.compose/service-bus` and the two connection-string environment variables defined in that workflow.

Both EF Core migration sets are intended to run from an approved deployment job. Terraform provisions databases and identities but does not execute schema migrations. Locally, migrations can be executed directly using `.\scripts\run-migrations.ps1`.

## Status

| Capability | Status |
| --- | --- |
| Microservices and service-owned databases | Implemented |
| Tenant/run and record-level idempotency | Implemented |
| Worker inbox, delivery ledger, and event outbox | Implemented |
| Unknown-outcome reconciliation workflow | Implemented |
| Record projection and premium operations console | Implemented |
| Versioned connection profiles and capability snapshots | Implemented |
| Confirmed-no-commit retry and read-after-write recovery | Implemented with simulated connector |
| Coalesced Key Vault cache and configurable vault routing | Implemented |
| Worker-executed connection tests with durable polling | Implemented |
| Entra SPA/API authentication and app-role authorization | Implemented & provisioned with claims mapping |
| Distributed traces, connector metrics, and business correlation | Implemented & verified live in App Insights |
| SQL/outbox readiness probe and Azure Monitor alerts | Implemented & provisioned in Azure |
| Per-connection concurrency limits, token bucket, safe retries, and circuit breaker | Implemented |
| Cursor-paged runs and records with status, connection, and date filtering | Implemented |
| Verified Blob archival, dry-run retention, and lifecycle tiering | Implemented; destructive mode disabled by default |
| Tenant-isolation, redelivery, retry-safety, circuit, archive-policy, and full Service Bus round-trip tests | Implemented |
| Safe poison-command classification and dead-letter verification | Implemented (ADR 0014) |
| Connector authentication strategies (ApiKey, Basic, OAuth2, MutualTls) | Implemented (Iteration 18) |
| Provider adapter architecture and dynamic adapter registry | Implemented (Iteration 19, ADR 0015) |
| Terraform state backend bootstrap with Entra auth & recovery protections | Applied (Iteration 17.5) |
| Terraform Azure environment with cost controls ($50 budget, 0.1GB quota, scale-to-zero) | Applied & live on Azure Container Apps (Iteration 17.5) |
| Dedicated Container Apps Job for private SQL EF migrations & user bootstrap | Implemented & executed (Iteration 17.5) |
| Real vendor connectors | Planned |
| Distributed limiter coordination and durable long-delay retries | Planned |

RelayWorks is a portfolio/reference implementation, not a production integration product.
