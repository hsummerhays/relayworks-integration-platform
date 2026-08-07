# ADR 0012: Keyset pagination for operational history

## Status

Accepted

## Context

Run and record history grows continuously. Returning all tenant data, or relying on an undocumented fixed cap, eventually causes database scans, API timeouts, large browser payloads, and excessive DOM rendering. Offset pagination also becomes slower on deep pages and can shift when new runs arrive.

## Decision

Run history is ordered by creation timestamp and ID descending. Record history is ordered by update timestamp and ID descending. Responses contain a bounded item collection and an opaque cursor encoding the last key pair. The next query applies a strict keyset boundary. Tenant scope is always supplied by authenticated context and is the leading index column.

Run filters support status, connection, and half-open UTC creation ranges. Record views support all records, attention-required records, and manually resolved records. Default page sizes are 25 runs and 50 records; both are capped at 100. The Vue console requests 25 records for a compact operator view and persists run filters in the URL, but not opaque traversal cursors.

## Consequences

Query cost remains proportional to page size rather than history depth, and newly inserted rows do not cause duplicate traversal through an existing cursor chain. The response does not include an expensive exact total count. Previous-page navigation is maintained by the client retaining cursors from its current browsing session. Changing filters begins a new cursor chain.
