# ADR 0006: Cached secret resolution and vault routing

## Status

Accepted — 2026-08-06

## Decision

Integration commands carry a structured secret locator containing vault URI, secret name, optional immutable version, and a routing key. They never carry credential values. The Worker resolves credentials through managed identity when it constructs one connector for a run.

Resolved values use a five-minute process-local cache keyed by vault, secret, and version. Concurrent misses are coalesced behind one asynchronous provider request. Provider failures trigger a ten-second cooldown to prevent a failing vault from producing a thundering herd. Cancellation by one waiter does not cancel the shared provider request.

An optional `SecretVaultRouting:{routingKey}` configuration override can redirect a tenant or region to another vault without changing contracts or persisted connection identities.

## Consequences

- A 5,000-record run performs one secret resolution, not 5,000.
- Each Worker replica maintains its own cache; Key Vault remains the authority.
- Versioned locators make rotations reproducible; unversioned locators refresh after TTL.
- Dedicated tenant or regional vaults can be introduced incrementally.
- Logs and metric dimensions identify only vault host and secret name where operationally necessary; secret values are never logged.
- Production should export the supplied .NET metrics through OpenTelemetry and alert on refresh failures and sustained misses.
