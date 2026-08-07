# RelayWorks failure and invariant matrix

| Failure or threat | Required behavior | Automated evidence | Remaining environment test |
| --- | --- | --- | --- |
| Duplicate Service Bus command | Do not call the destination twice | Unit test plus `BrokerRedeliveryPreservesTheDurableDeliveryGate` over the Service Bus Emulator | Replay a real locked message after Worker restart |
| Timeout after destination send | Mark `UnknownOutcome`; never retry | `Never_retries_an_unknown_outcome` | Inject network loss after provider accepts request |
| Confirmed HTTP 429/no commit | Honor `Retry-After` and retry within bound | `Retries_only_confirmed_no_commit_and_honors_retry_after` | Provider sandbox quota test |
| Repeated confirmed failures | Open circuit and stop destination traffic | `Open_circuit_stops_calls_after_confirmed_failure_threshold` | Observe Azure Monitor circuit metric and recovery |
| Worker crash after ledger gate | Lookup if supported; otherwise require reconciliation | `Read_after_write_recovers_an_ambiguous_committed_record` | Kill Container App during a real connector write |
| Concurrent Key Vault demand | Coalesce requests and avoid a secret-fetch storm | `Concurrent_record_volume_causes_one_vault_request` | Load test using managed identity and Key Vault metrics |
| Cross-tenant run query | Return only authenticated tenant data | `Run_listing_never_returns_another_tenants_rows` | Repeat with signed Entra tokens and every endpoint |
| Malformed archive candidate | Never archive active/recent/unresolved work | `ArchivePolicyTests` | Dry-run review against production-shaped data |
| Blob upload/verification failure | Keep all SQL source rows | Export-before-delete workflow and dry-run default | Deny Blob write/read during an archive cycle |
| Crash after Blob upload | Safely replace deterministic blob; retain SQL until verified | Deterministic path, Blob versioning, verification gate | Kill Control Plane between upload and SQL transaction |
| Pending outbox row | Never delete before dispatch | Retention predicate requires `DispatchedAtUtc` | Seed aged pending/dispatched rows in Azure SQL |

## Test levels

- Unit tests prove domain state transitions, cursor encoding, archive eligibility, retry classification, cache coalescing, and circuit behavior.
- In-process integration tests exercise HTTP routing, tenant context, authorization configuration, EF queries, and response contracts.
- Azure failure drills remain necessary for managed identity, private DNS, Service Bus locks, SQL transactions, Blob versioning, and actual vendor semantics. These drills belong in a disposable environment, never a customer production tenant.

The CI workflow restores, builds, and tests the complete solution before separately building Vue and validating Terraform. A failure drill is not considered passed merely because a unit test models it; the final column identifies infrastructure behavior that requires a deployed environment.

## Service Bus end-to-end lane

The separate `service-bus-e2e.yml` workflow starts the Azure Service Bus Emulator and its SQL Server dependency, executes the production Worker command consumer and outbox publisher, and verifies inbox, delivery-ledger, result-event, completion-event, and redelivery behavior. It remains outside the fast unit-test lane. A deployed-environment drill is still required to validate Azure Service Bus lock loss, managed identity, networking, and restart behavior that the emulator does not reproduce.
