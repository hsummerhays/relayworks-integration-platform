# Changelog

## Iteration 15 — 2026-08-07

- Added a dedicated Azure Service Bus Emulator end-to-end test lane, isolated from the fast unit-test suite.
- Exercised the production command worker and Worker outbox publisher over real AMQP messaging with a relational SQL ledger.
- Verified command JSON compatibility, inbox persistence, delivery-ledger persistence, record-result and completion events, and redelivery idempotency.
- Added deterministic emulator entities, health gating, failure log capture, bounded CI execution, and unconditional container cleanup.

## Post-Iteration 14 maintenance — 2026-08-07

- Replaced process-global environment-variable mutations in Control Plane integration tests with environment-specific `appsettings.Testing.json`.
- Preserved parallel test execution while ensuring authentication, Service Bus, and archival branches are disabled before application service registration.
- Added the Azure Service Bus Emulator end-to-end flow to the post-reference-platform test roadmap.
- Corrected integration-run construction while preserving the connector-profile snapshot on the queued command.

## Iteration 14 — 2026-08-07

- Replaced EF InMemory API coverage with SQL Server Testcontainers and real EF migrations.
- Added SQL translation and HTTP tenant-isolation coverage against the production database engine.
- Split run and record archive eligibility for clear short-circuit behavior.
- Centralized integration-record status constants across archive, persistence, API, and tests.
- Added a serializable final eligibility guard immediately before archive deletion.
- Added CI time bounds, a protected deployment-plan workflow, immutable plan artifact, and readiness smoke-test script.
- Raised Microsoft.OpenApi to the advisory's patched 2.7.5 floor and Microsoft.Identity.Web to 4.14.2; removed unnecessary framework-package pins that triggered NU1510. NuGet Audit remains enforced.

## Iteration 13 — 2026-08-07

- Added an in-process Control Plane integration-test host using an isolated EF Core database.
- Added an HTTP-level test proving one tenant cannot list another tenant's integration runs.
- Added archive-policy tests proving active, recent, rejected, and unknown-outcome work cannot be archived.
- Added a circuit-breaker failure-injection test proving calls stop after the configured threshold.
- Retained and organized existing tests for command redelivery, unknown-outcome non-retry, read-after-write recovery, and Key Vault request coalescing.
- Added a failure matrix that maps each safety invariant to its automated or deployment-level verification.

## Iteration 12 — 2026-08-07

- Added a Control Plane archival worker with dry-run mode and safe minimum retention validation.
- Exported eligible terminal runs and record projections as compressed, schema-versioned JSON in tenant/year/month Blob partitions.
- Added SHA-256 and byte-length verification plus a separate manifest before SQL deletion.
- Excluded active runs and every run containing unresolved rejected or unknown-outcome records.
- Added conservative cleanup for dispatched outboxes and completed Worker inbox rows.
- Preserved the Worker delivery ledger as the long-lived duplicate-prevention tombstone.
- Provisioned private, managed-identity-only Azure Blob storage with versioning, soft delete, and cool/archive lifecycle tiers.
- Added archive/retention metrics, system audit records, Terraform controls, ADR 0013, and recovery guidance.

## Iteration 11 — 2026-08-07

- Replaced the implicit 200-run cap with explicit cursor-paged response envelopes.
- Added tenant-scoped run filtering by status, connection, and UTC creation range.
- Added cursor pagination and server-side all/attention/resolved filtering for record projections.
- Added bounded page sizes and invalid-cursor/date-range validation.
- Added composite SQL indexes and an EF Core migration for the new query shapes.
- Added Vue run filters, forward/back paging, responsive controls, and URL-persisted filter state.
- Added cursor round-trip and page-boundary application tests.

## Iteration 10 — 2026-08-07

- Converted destination writes and idempotency lookups to cancellation-aware asynchronous connector operations.
- Added a shared per-connection concurrency gate and token-bucket request limiter across concurrent Worker runs.
- Added bounded exponential backoff with jitter and provider `Retry-After` support.
- Preserved the financial-safety boundary: only `ConfirmedNoCommit` results are retried; `UnknownOutcome` is never retried.
- Added a per-connection circuit breaker for repeated confirmed failures.
- Added low-cardinality retry, throttle-wait, and circuit-open metrics.
- Exposed rate and concurrency controls through Terraform and added focused retry-safety tests.

## Iteration 9 — 2026-08-06

- Added OpenTelemetry traces and metrics to the Control Plane and Sync Worker with conditional Azure Monitor export.
- Propagated W3C trace context and business correlation IDs over Service Bus commands and events.
- Instrumented outbox publishing and lag, message consumption, connector duration, record outcomes, and projections.
- Added SQL and command-outbox readiness checks and Azure Container App health probes.
- Added Azure Monitor alerts for Service Bus dead letters and application exceptions with an operations action group.
- Added an operations runbook with KQL triage queries and explicit alert response procedures.

## Iteration 8 — 2026-08-06

- Added MSAL browser authentication and silent API token acquisition to the Vue console.
- Added interactive token recovery, sign-in/sign-out states, and authenticated operator/tenant display.
- Removed tenant IDs from mutation request contracts; tenant scope now comes only from the signed access token.
- Added `Integration.Operator` and `Integration.Admin` server authorization policies.
- Restricted connection creation and audit history to administrators and operational mutations to operators.
- Prevented authenticated API failures from falling back to representative demo data.
- Added Terraform inputs and build outputs for the SPA client, API audience, tenant, and scope.

## Iteration 7 — 2026-08-06

- Added durable asynchronous connection-test requests and terminal result projections.
- Saved connection-test state, command outbox, and operator audit in one Control Plane transaction.
- Executed tests in the Worker through the real cached-secret and connector-construction path.
- Added a 30-second Worker timeout and sanitized credential, provider, network, and configuration failure categories.
- Added idempotent Worker inbox handling and result outbox publishing.
- Added Vue polling every two seconds, local timeout behavior, persisted latest-result reload, and premium status treatment.

## Iteration 6 — 2026-08-06

- Added Microsoft Entra ID bearer authentication with a secure fallback authorization policy.
- Derived tenant identity from a signed token claim instead of trusting query or request values.
- Enforced tenant filters across runs, record details, connections, reconciliation, and audit APIs.
- Added immutable operator audit records for connection creation and manual reconciliation.
- Added development-only authentication bypass with a fixed configured tenant.
- Added Terraform inputs and Container App settings for the API app registration.

## Iteration 5 — 2026-08-06

- Replaced opaque secret references in execution messages with structured vault, name, version, and routing fields.
- Added Azure Key Vault secret resolution using managed identity.
- Added a five-minute in-memory cache with concurrent request coalescing and a short failure cooldown.
- Added cache hit, miss, and refresh-failure metrics without tenant or secret values as dimensions.
- Added configurable tenant/region vault routing while retaining a shared-vault default.
- Changed Worker execution to construct one credentialed destination connector per run.
- Added a 5,000-request concurrency test that produces one underlying secret-provider call.
- Added masked credential status to the connection-management console.

## Iteration 4 — 2026-08-06

- Added tenant-scoped connection profiles with versioned capability snapshots on run commands.
- Distinguished confirmed no-commit failures from unknown outcomes and bounded only safe retries.
- Added read-after-write reconciliation for connectors that can prove an ambiguous write committed.
- Added Key Vault secret references without placing credentials in messages or configuration records.
- Granted the Worker managed identity least-privilege Key Vault secret access in Terraform.
- Added a connector registry and capability editor to the Vue operations console.
- Added a recovery test for ambiguous writes confirmed by destination lookup.

## Iteration 3 — 2026-08-06

- Added a Worker-owned Azure SQL database with a durable processed-record ledger, inbox, and outbox.
- Added a unique record delivery gate and canonical fingerprint to prevent duplicate destination writes.
- Added explicit `UnknownOutcome` handling; ambiguous writes stop and are never auto-retried.
- Added versioned record-result events, Control Plane projections, and reconciliation APIs.
- Added a separate Worker database to Terraform and a record-level operations console.
- Added a redelivery test proving the destination connector is not invoked twice.

## Iteration 2 — 2026-08-06

- Split the runtime into independently deployable Control Plane and Sync Worker services.
- Added versioned integration messages and a canonical time-entry contract.
- Replaced in-memory persistence with EF Core/Azure SQL configuration and an initial migration.
- Added tenant-scoped database idempotency and concurrent-duplicate recovery.
- Added a transactional outbox, Service Bus publisher, command worker, and result consumer.
- Added simulated field-operations and accounting connector boundaries.
- Moved CORS, SQL, and Service Bus settings into configuration.
- Added Terraform for private-networked Azure Container Apps, Azure SQL, Service Bus, identities, registry, Vue hosting, Key Vault, and observability.
- Added Dockerfiles, CI validation, ADRs, and Worker tests.

## Iteration 1 — 2026-08-06

- Created the RelayWorks repository, integration-run domain, in-memory API slice, and Vue operations console.
