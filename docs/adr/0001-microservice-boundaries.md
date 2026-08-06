# ADR 0001: Begin with Control Plane and Sync Worker services

## Status

Accepted for Iteration 2.

## Decision

RelayWorks starts with two deployables: a Control Plane that owns integration configuration and durable run state, and a Sync Worker that owns connector execution. Connector adapters remain within the Worker until scaling, security, networking, or release independence justifies extraction.

## Consequences

- The async boundary and failure modes are real and demonstrable.
- The services share versioned contracts but not databases or domain implementations.
- Azure Service Bus and eventual consistency add operational complexity.
- The platform avoids a premature service per vendor or document type.
