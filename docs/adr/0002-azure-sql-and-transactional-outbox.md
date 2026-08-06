# ADR 0002: Use Azure SQL and a transactional outbox

## Status

Accepted for Iteration 2.

## Decision

The Control Plane stores runs and outbox messages in Azure SQL through EF Core. Creating a run and its command message is one database transaction. A hosted publisher later sends undispatched messages to Service Bus.

## Consequences

- Relational uniqueness enforces tenant-scoped idempotency.
- Run state and intended publication cannot diverge during the initial write.
- Publication is at-least-once and consumers must be idempotent.
- The outbox requires monitoring, retry limits, and eventual cleanup.
