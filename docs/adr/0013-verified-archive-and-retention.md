# ADR 0013: Verified archive and conservative retention

## Status

Accepted

## Context

Record projections, inboxes, and outboxes grow continuously, but deleting them uniformly would remove operational history and could weaken duplicate-write protection. Archival crosses SQL and Blob Storage, which cannot participate in one atomic transaction.

## Decision

The Control Plane owns archival of its runs and record projections. Eligible runs must be terminal, older than the configured retention period (minimum 30 days), and contain no `Rejected` or `UnknownOutcome` records. Each run is serialized with a schema version, compressed, partitioned by tenant/year/month, and uploaded to private Blob Storage. RelayWorks sets and re-reads a SHA-256 content hash and compressed length, then writes a manifest. Only after verification does it delete the projection and run in a SQL transaction and append a system audit record.

The export path is deterministic and uploads are replaceable, making a crash before SQL deletion safely repeatable. Blob versioning and soft deletion retain prior writes. Dry-run is the default. Dispatched outbox rows and completed inbox rows have separate conservative retention windows. Pending outbox rows are never deleted.

Worker `RecordDelivery` rows are not deleted. They remain compact idempotency tombstones after the richer Control Plane history is archived. A future policy may move bulky error text out of old terminal ledger rows, but must preserve the unique delivery key, canonical fingerprint, state, and destination reference.

## Consequences

SQL operational tables remain bounded while the duplicate-prevention invariant survives historical source replays. Archive consumers must understand schema versions. Blob and SQL cannot be committed atomically, so the workflow intentionally favors duplicate archive versions over data loss. Restoring an archive is an explicit administrative process rather than an automatic API fallback.
