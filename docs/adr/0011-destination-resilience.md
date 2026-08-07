# ADR 0011: Connection-scoped destination resilience

## Status

Accepted

## Context

Multiple runs can target the same construction ERP concurrently. Process-level concurrency alone cannot prevent those runs from exceeding a provider quota or overloading an older customer-hosted system. Conventional HTTP retries are unsafe for time-entry and financial writes when the destination may have committed before a timeout.

## Decision

The Sync Worker applies a shared concurrency gate and token bucket keyed by connection-profile ID. Connector calls are asynchronous and cancellation-aware. A provider's `Retry-After` value takes precedence over exponential backoff with jitter when it requests a longer delay. Repeated confirmed failures open a short per-connection circuit.

Only the connector result `ConfirmedNoCommit` is eligible for automatic retry. HTTP status alone does not establish this guarantee: each production connector must map its provider contract deliberately. `UnknownOutcome` remains terminal pending read-after-write proof or human reconciliation.

Tenant and connection IDs are excluded from metrics. Provider, outcome, and bounded reason categories are permitted. Configuration defaults are conservative and may be overridden by deployment settings.

## Consequences

Concurrent runs share destination capacity and avoid synchronized retry storms. One connection cannot throttle unrelated customer connections. The in-memory gate is replica-local, so Terraform constrains the Worker to one replica. Horizontal scaling requires a distributed lease/token store before that cap is raised. Long delays are not held in memory indefinitely; durable scheduled command/record retries remain a separate design step requiring persisted retry state.
