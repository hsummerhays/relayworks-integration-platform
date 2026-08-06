# Changelog

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
