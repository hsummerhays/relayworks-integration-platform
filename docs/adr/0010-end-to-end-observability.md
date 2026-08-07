# ADR 0010: End-to-end observability

## Status

Accepted

## Context

Integration failures cross HTTP, two service-owned databases, Service Bus, and external connectors. A run ID alone cannot explain infrastructure latency, while trace context alone is inconvenient for operator searches and reconciliation.

## Decision

Both services emit OpenTelemetry traces and low-cardinality metrics. W3C `traceparent` and `tracestate` values travel in Service Bus application properties. Messages also carry their run or connection-test ID as the Service Bus correlation ID. Azure Monitor is the production exporter when an Application Insights connection string is configured.

Health traffic is excluded from tracing. Metrics may use service, provider, operation, outcome, and message type dimensions, but never tenant IDs, source record IDs, secret references, or credential values. The public readiness probe verifies Control Plane SQL connectivity and treats command-outbox lag over 60 seconds as degraded.

## Consequences

Operators can follow a request across asynchronous boundaries and search by a stable business identifier. Alert dimensions remain bounded as tenant count grows. Service Bus dead letters and application exceptions page the configured action group. Connector diagnostics still require disciplined exception sanitization, and telemetry is not an audit record or a substitute for the durable delivery ledger.
