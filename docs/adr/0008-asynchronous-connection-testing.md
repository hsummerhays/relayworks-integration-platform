# ADR 0008: Durable asynchronous connection testing with polling

## Status

Accepted — 2026-08-06

## Decision

`POST /api/connections/{id}/tests` persists a tenant-scoped `Pending` test and Service Bus command outbox atomically, then returns `202 Accepted` with the status URL. The Sync Worker resolves the actual Key Vault credential, constructs the configured connector, and executes its lightweight health check with a 30-second timeout. A versioned result event updates the Control Plane projection.

The Vue console polls the status resource every two seconds. It stops for `Succeeded`, `Failed`, `TimedOut`, or `Canceled`. After one minute it stops polling locally without canceling server work, explains that processing may continue, and reloads the most recent durable result when connections are revisited.

## Consequences

- The Control Plane never performs customer-system calls.
- Tests exercise the same identity, network path, secret cache, and connector factory used by integrations.
- Sanitized failure categories reach operators; raw exceptions remain in Worker diagnostics.
- Polling avoids SignalR infrastructure until multiple long-running workflows justify push notifications.
- Duplicate command delivery is harmless through the Worker inbox and outbox.
