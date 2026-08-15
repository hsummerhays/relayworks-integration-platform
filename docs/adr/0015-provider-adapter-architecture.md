# ADR 0015: Provider adapter architecture and adapter registry

## Status

Accepted — 2026-08-15

## Context

Prior to this decision, connector resolution was implemented via provider-name switch blocks inside `TimeEntryDestinationConnectorFactory`, mixing credential resolution, authentication mechanisms, provider selection, and connector instantiation. Additionally, concrete connectors directly coupled transport and configuration details rather than operating through standardized adapter contracts.

## Decision

We introduce a formal provider-neutral adapter architecture:

1. **Adapter Abstractions**:
   - `IIntegrationAdapter`: Base adapter interface exposing `Provider`, `ConnectorCapabilities`, and `TestConnectionAsync`.
   - `ITimeEntrySourceAdapter`: Source contract providing `Read(TimeEntryReadRequest, ConnectorContext)`.
   - `ITimeEntryDestinationAdapter`: Destination contract providing `WriteAsync` and `FindExistingAsync` (read-after-write).

2. **Decoupled Connector Context (`ConnectorContext`)**:
   - Adapters receive execution details through `ConnectorContext`, containing tenant identity, connection parameters, snapshotted capabilities, and the configured `IConnectorAuthenticator`.
   - Adapters are pure integration components and have no dependency on Key Vault, cloud secret stores, or outbox persistence.

3. **Dynamic Adapter Registry (`IAdapterRegistry`)**:
   - Replaced central provider `switch` statements with `IAdapterRegistry`, allowing source and destination adapters to register declaratively into the DI container.

4. **Resilience Separation**:
   - Adapters report objective outcome states (`Succeeded`, `Rejected`, `ConfirmedNoCommit`, `UnknownOutcome`) and never perform retries directly.
   - Centralized `DestinationResilienceExecutor` retains full ownership of circuit breaking, backoff, and idempotent retry policies.

## Consequences

- New vendor adapters (e.g. Sage 100/300, QuickBooks Desktop, Procore) can be added simply by implementing `ITimeEntryDestinationAdapter` and registering with the DI container.
- Platform resilience, deduplication, and outbox semantics remain strictly outside individual adapters.
- Integration tests can verify any adapter against standard contract test fixtures.
