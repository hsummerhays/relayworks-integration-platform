# Changelog

## Iteration 6 — 2026-08-06

- Added Microsoft Entra ID bearer authentication with a secure fallback authorization policy.
- Derived tenant identity from a signed token claim instead of trusting query or request values.
- Enforced tenant filters across runs, record details, connections, reconciliation, and audit APIs.
- Added immutable operator audit records for connection creation and manual reconciliation.
- Added development-only authentication bypass with a fixed configured tenant.
- Added Terraform inputs and Container App settings for the API app registration.

## Iteration 5 — 2026-08-06

- Replaced opaque secret references in execution messages with structured vault, name, version, and routing fields.
- Added Azure Key Vault secret resolution using managed identity.
- Added a five-minute in-memory cache with concurrent request coalescing and a short failure cooldown.
- Added cache hit, miss, and refresh-failure metrics without tenant or secret values as dimensions.
- Added configurable tenant/region vault routing while retaining a shared-vault default.
- Changed Worker execution to construct one credentialed destination connector per run.
- Added a 5,000-request concurrency test that produces one underlying secret-provider call.
- Added masked credential status to the connection-management console.

## Iteration 4 — 2026-08-06

- Added tenant-scoped connection profiles with versioned capability snapshots on run commands.
- Distinguished confirmed no-commit failures from unknown outcomes and bounded only safe retries.
- Added read-after-write reconciliation for connectors that can prove an ambiguous write committed.
- Added Key Vault secret references without placing credentials in messages or configuration records.
- Granted the Worker managed identity least-privilege Key Vault secret access in Terraform.
- Added a connector registry and capability editor to the Vue operations console.
- Added a recovery test for ambiguous writes confirmed by destination lookup.

## Iteration 3 — 2026-08-06

- Added a Worker-owned Azure SQL database with a durable processed-record ledger, inbox, and outbox.
- Added a unique record delivery gate and canonical fingerprint to prevent duplicate destination writes.
- Added explicit `UnknownOutcome` handling; ambiguous writes stop and are never auto-retried.
- Added versioned record-result events, Control Plane projections, and reconciliation APIs.
- Added a separate Worker database to Terraform and a record-level operations console.
- Added a redelivery test proving the destination connector is not invoked twice.

## Iteration 2 — 2026-08-06

- Split the runtime into independently deployable Control Plane and Sync Worker services.
- Added versioned integration messages and a canonical time-entry contract.
- Replaced in-memory persistence with EF Core/Azure SQL configuration and an initial migration.
- Added tenant-scoped database idempotency and concurrent-duplicate recovery.
- Added a transactional outbox, Service Bus publisher, command worker, and result consumer.
- Added simulated field-operations and accounting connector boundaries.
- Moved CORS, SQL, and Service Bus settings into configuration.
- Added Terraform for private-networked Azure Container Apps, Azure SQL, Service Bus, identities, registry, Vue hosting, Key Vault, and observability.
- Added Dockerfiles, CI validation, ADRs, and Worker tests.

## Iteration 1 — 2026-08-06

- Created the RelayWorks repository, integration-run domain, in-memory API slice, and Vue operations console.
