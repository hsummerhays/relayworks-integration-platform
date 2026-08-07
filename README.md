# RelayWorks Integration Platform

RelayWorks is a .NET and Vue reference platform for reliable construction-system integrations. Iteration 14 hardens relational testing, archive concurrency safety, CI, and approval-gated deployment planning.

## Implemented vertical slice

1. An operator submits a tenant-scoped time-entry export through the Vue console.
2. The Control Plane saves the run and command outbox in one Azure SQL transaction.
3. Azure Service Bus delivers `IntegrationRunRequestedV1` at least once.
4. Before each destination call, the Worker acquires a unique record delivery gate in its own database.
5. The Worker persists record outcomes and versioned result events in one database transaction.
6. The Control Plane builds an operator-facing projection of rejected and ambiguous records.
7. `UnknownOutcome` records stop until a human verifies the destination and documents resolution.

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

Both EF Core migration sets are intended to run from an approved deployment job. Terraform provisions databases and identities but does not execute schema migrations.

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
| Entra SPA/API authentication and app-role authorization | Implemented; registrations not provisioned |
| Distributed traces, connector metrics, and business correlation | Implemented |
| SQL/outbox readiness probe and Azure Monitor alerts | Implemented, not applied |
| Per-connection concurrency limits, token bucket, safe retries, and circuit breaker | Implemented |
| Cursor-paged runs and records with status, connection, and date filtering | Implemented |
| Verified Blob archival, dry-run retention, and lifecycle tiering | Implemented; destructive mode disabled by default |
| Tenant-isolation, redelivery, retry-safety, circuit, and archive-policy tests | Implemented |
| Terraform Azure environment | Implemented, not applied |
| Real vendor connectors | Planned |
| Distributed limiter coordination and durable long-delay retries | Planned |

RelayWorks is a portfolio/reference implementation, not a production integration product.
