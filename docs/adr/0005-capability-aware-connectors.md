# ADR 0005: Capability-aware connector execution

## Status

Accepted — 2026-08-06

## Decision

Each tenant connection records the destination provider, supported idempotency and read-after-write capabilities, a bounded confirmed-no-commit retry limit, and a Key Vault secret reference. The Control Plane snapshots those non-secret settings into `IntegrationRunRequestedV1` so execution remains reproducible and the Worker never queries the Control Plane database.

Connector failures are classified by evidence:

- `ConfirmedNoCommit` proves the destination did not commit and may be retried within the snapshotted limit.
- `UnknownOutcome` may be converted to success only when a supported lookup proves the record exists.
- An unknown outcome without proof remains stopped for manual reconciliation.

## Consequences

- Connector adapters must declare actual capabilities rather than inherit optimistic platform defaults.
- Editing a connection does not change an already-submitted run.
- Secret values remain in Key Vault; messages contain references only.
- Retry count is a connection policy but is applied only to confirmed-no-commit outcomes.
- Provider-specific lookup behavior belongs inside the connector adapter.
