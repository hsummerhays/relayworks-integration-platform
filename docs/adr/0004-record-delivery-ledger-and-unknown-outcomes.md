# ADR 0004: Worker-owned record delivery ledger and unknown outcomes

## Status

Accepted — 2026-08-06

## Decision

The Sync Worker owns a separate Azure SQL database containing its command inbox, record-delivery ledger, and event outbox. A unique key across tenant, connection, operation, source record, and source version is acquired before invoking a destination connector. A SHA-256 fingerprint detects a source-version contract violation.

`Succeeded`, `Rejected`, and `UnknownOutcome` are terminal delivery states. `UnknownOutcome` means the connector cannot prove whether the destination committed a write. RelayWorks will not retry that record automatically. An operator must inspect the destination and record a manual resolution in the Control Plane projection.

## Consequences

- At-least-once command delivery does not cause a second connector call for the same record identity.
- A crash after acquiring the delivery gate is conservatively projected as `UnknownOutcome` on redelivery.
- The Control Plane does not query the Worker database.
- Manual resolution is operational state and does not mutate the Worker ledger.
- Ledger keys must be retained for at least the destination's financial correction window.
