# ADR 0014: Classify invalid commands before business processing

## Status

Accepted

## Context

Service Bus redelivery is valuable for transient infrastructure failures, but it cannot repair an unknown command type, malformed JSON, or a command missing required identifiers and its connector-profile snapshot. Repeatedly executing those messages consumes capacity, delays valid tenant work, and obscures the operational cause.

## Decision

The Sync Worker validates the message subject, JSON envelope, required identifiers, positive record count, and connector profile before resolving connectors or writing to its ledger. Unknown subjects are dead-lettered as `UnsupportedCommandType`. Structurally invalid supported commands are dead-lettered as `InvalidCommandPayload` with a fixed safe description. Payload contents and serializer details are not copied into the broker description or logs.

Transient failures after a valid command crosses this boundary retain normal Service Bus retry behavior. Unsupported operations remain separately classified as `UnsupportedOperation`.

## Consequences

- Poison commands consume one delivery instead of exhausting the queue delivery limit.
- Operators receive stable, searchable reason codes without credential or construction-record leakage.
- Producers must treat dead-letter reason codes as an integration contract and alert on their occurrence.
- Broker-level tests must prove invalid messages do not create inbox, ledger, outbox, or destination side effects.
