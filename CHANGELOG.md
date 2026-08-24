# Changelog

## Maintenance — 2026-08-24: Infrastructure Variable Sanitization & PII Removal

- Sanitized Terraform defaults across `infra/modules/relayworks-platform` and `infra/environments/dev` by replacing tenant-specific Entra ID URI and email defaults with generic placeholders.
- Updated `terraform.tfvars.example` to document configurable API identifier URI and budget notification email variables.

## Iteration 20 — 2026-08-16: Local Development Automation & Modern PHP Portal

- Built unified PowerShell development scripts (`start-local-dev.ps1`, `stop-local-dev.ps1`, `run-migrations.ps1`, `start-php-portal.ps1`, `test-php-portal.ps1`) managing container lifecycle, SQL Server readiness polling, migration execution, and parallel application processes.
- Created modern, server-side-rendered PHP 8.3 reference operations portal (`web/relayworks-portal-php`) with strict typing, PSR-4 routing, zero external dependencies, dark-mode CSS theme, and connection probe tester.
- Updated ASP.NET Core Control Plane API network bindings to `http://0.0.0.0:5080` to support containerized host gateway communication (`host.docker.internal`).
- Documented Azure cloud startup and runtime lifecycle architecture (Managed Identities, Container Apps Jobs, Service Bus RBAC, and cloud vs. local topology comparison).

## Iteration 19 — 2026-08-15: Provider Adapter Architecture

- Established a formal provider adapter architecture with `IIntegrationAdapter`, `ITimeEntrySourceAdapter`, and `ITimeEntryDestinationAdapter`.
- Decoupled transport, tenant identity, and authentication from adapter behavior using `ConnectorContext` and `ConnectorCapabilities`.
- Added registration-based `IAdapterRegistry` replacing central switch blocks and enabling dynamic provider registration.
- Preserved centralized idempotency, delivery ledger, and circuit breaking in `DestinationResilienceExecutor` outside adapter code.
- Added contract test suite (`AdapterRegistryAndContractTests`) and documented architecture boundaries in ADR 0015.
- Implemented transport-level mTLS client certificate configuration on `MutualTlsAuthenticator` using `X509CertificateLoader`.

## Iteration 18 — 2026-08-15: Connector Authentication Strategies

- Separated connector authentication strategies from connector domain behavior.
- Added `ConnectorAuthenticationType` enum (`ApiKey`, `Basic`, `OAuth2`, `MutualTls`) to `ConnectorExecutionProfileV1`, `ConnectionProfile`, and the database schema with a safe `ApiKey` migration default.
- Added `IConnectorAuthenticator` strategy implementations with typed secret payloads (`ApiKeyCredential` with configurable header names, `BasicAuthCredential`, `OAuth2ClientCredential`, `MutualTlsCredential`).
- Implemented `OAuth2TokenAuthenticator` with proactive expiration caching, concurrent refresh coalescing, sanitized error logging, and tenant/configuration scope isolation in cache keys.
- Added independent `IConnectorAuthenticatorFactory` and wired it into connector adapters.
- Extended connection test verification to validate configured authentication flows.
- Updated Vue console connection manager with authentication strategy selector and capability badges.
- Added unit and strategy tests covering token coalescing, scope isolation, expiration caching, secret redaction, and factory resolution.

## Iteration 17.5 — 2026-08-15: Azure Container Apps Migration Job, Staged Deployment, and Live Documentation

- Added Azure Container Apps dedicated migration job for private SQL EF migrations and user bootstrap.
- Configured Terraform state backend bootstrap with Entra authentication and recovery protections.
- Enforced cloud infrastructure cost controls ($50 budget, 0.1GB quota, scale-to-zero).
- Provisioned live deployment validation and smoke-test workflows.

## Iteration 17 — 2026-08-07: Poison-Message Classification and Safe Dead-Letter Handling

- Added explicit poison-command classification at the Worker broker boundary.
- Dead-lettered unknown subjects as `UnsupportedCommandType` and malformed or incomplete supported commands as `InvalidCommandPayload` without retrying deterministic failures.
- Kept payload contents and serializer details out of dead-letter descriptions and structured logs.
- Extended the Service Bus emulator round trip to verify both dead-letter reasons and the absence of ledger or destination-write side effects.
- Documented the retry-versus-dead-letter boundary in ADR 0014.

## Iteration 16 — 2026-08-07: Complete Control Plane–Worker Broker Round Trip

- Extended the Service Bus emulator lane through the production Control Plane result consumer.
- Added a separate relational Control Plane database to the E2E topology and seeded the source run before command delivery.
- Verified that Worker record and completion events create the operator record projection and transition the run to `Completed` with exact accepted/rejected totals.
- Replayed the logical work through both the command inbox and durable record ledger while confirming the Control Plane projection remains idempotent.

## Iteration 15 — 2026-08-07: Azure Service Bus Emulator End-to-End Testing

- Added a dedicated Azure Service Bus Emulator end-to-end test lane, isolated from the fast unit-test suite.
- Exercised the production command worker and Worker outbox publisher over real AMQP messaging with a relational SQL ledger.
- Verified command JSON compatibility, inbox persistence, delivery-ledger persistence, record-result and completion events, and redelivery idempotency.
- Added deterministic emulator entities, health gating, failure log capture, bounded CI execution, and unconditional container cleanup.

## Post-Iteration 14 maintenance — 2026-08-07

- Replaced process-global environment-variable mutations in Control Plane integration tests with environment-specific `appsettings.Testing.json`.
- Preserved parallel test execution while ensuring authentication, Service Bus, and archival branches are disabled before application service registration.
- Added the Azure Service Bus Emulator end-to-end flow to the post-reference-platform test roadmap.
- Corrected integration-run construction while preserving the connector-profile snapshot on the queued command.

## Iteration 14 — 2026-08-07: Production-Database Integration Testing and Deployment Hardening

- Replaced EF InMemory API coverage with SQL Server Testcontainers and real EF migrations.
- Added SQL translation and HTTP tenant-isolation coverage against the production database engine.
- Split run and record archive eligibility for clear short-circuit behavior.
- Centralized integration-record status constants across archive, persistence, API, and tests.
- Added a serializable final eligibility guard immediately before archive deletion.
- Added CI time bounds, a protected deployment-plan workflow, immutable plan artifact, and readiness smoke-test script.
- Raised Microsoft.OpenApi to the advisory's patched 2.7.5 floor and Microsoft.Identity.Web to 4.14.2; removed unnecessary framework-package pins that triggered NU1510. NuGet Audit remains enforced.

## Iteration 13 — 2026-08-07: Integration, Safety-Invariant, and Failure-Injection Testing

- Added an in-process Control Plane integration-test host using an isolated EF Core database.
- Added an HTTP-level test proving one tenant cannot list another tenant's integration runs.
- Added archive-policy tests proving active, recent, rejected, and unknown-outcome work cannot be archived.
- Added a circuit-breaker failure-injection test proving calls stop after the configured threshold.
- Retained and organized existing tests for command redelivery, unknown-outcome non-retry, read-after-write recovery, and Key Vault request coalescing.
- Added a failure matrix that maps each safety invariant to its automated or deployment-level verification.

## Iteration 12 — 2026-08-07: Verified Archival and Data Retention

- Added a Control Plane archival worker with dry-run mode and safe minimum retention validation.
- Exported eligible terminal runs and record projections as compressed, schema-versioned JSON in tenant/year/month Blob partitions.
- Added SHA-256 and byte-length verification plus a separate manifest before SQL deletion.
- Excluded active runs and every run containing unresolved rejected or unknown-outcome records.
- Added conservative cleanup for dispatched outboxes and completed Worker inbox rows.
- Preserved the Worker delivery ledger as the long-lived duplicate-prevention tombstone.
- Provisioned private, managed-identity-only Azure Blob storage with versioning, soft delete, and cool/archive lifecycle tiers.
- Added archive/retention metrics, system audit records, Terraform controls, ADR 0013, and recovery guidance.

## Iteration 11 — 2026-08-07: Keyset Pagination and Operational Filtering

- Replaced the implicit 200-run cap with explicit cursor-paged response envelopes.
- Added tenant-scoped run filtering by status, connection, and UTC creation range.
- Added cursor pagination and server-side all/attention/resolved filtering for record projections.
- Added bounded page sizes and invalid-cursor/date-range validation.
- Added composite SQL indexes and an EF Core migration for the new query shapes.
- Added Vue run filters, forward/back paging, responsive controls, and URL-persisted filter state.
- Added cursor round-trip and page-boundary application tests.

## Iteration 10 — 2026-08-07: Destination Resilience, Rate Limiting, and Circuit Breaking

- Converted destination writes and idempotency lookups to cancellation-aware asynchronous connector operations.
- Added a shared per-connection concurrency gate and token-bucket request limiter across concurrent Worker runs.
- Added bounded exponential backoff with jitter and provider `Retry-After` support.
- Preserved the financial-safety boundary: only `ConfirmedNoCommit` results are retried; `UnknownOutcome` is never retried.
- Added a per-connection circuit breaker for repeated confirmed failures.
- Added low-cardinality retry, throttle-wait, and circuit-open metrics.
- Exposed rate and concurrency controls through Terraform and added focused retry-safety tests.

## Iteration 9 — 2026-08-06: End-to-End Observability and Operational Alerting

- Added OpenTelemetry traces and metrics to the Control Plane and Sync Worker with conditional Azure Monitor export.
- Propagated W3C trace context and business correlation IDs over Service Bus commands and events.
- Instrumented outbox publishing and lag, message consumption, connector duration, record outcomes, and projections.
- Added SQL and command-outbox readiness checks and Azure Container App health probes.
- Added Azure Monitor alerts for Service Bus dead letters and application exceptions with an operations action group.
- Added an operations runbook with KQL triage queries and explicit alert response procedures.

## Iteration 8 — 2026-08-06: Vue Console Authentication and App-Role Authorization

- Added MSAL browser authentication and silent API token acquisition to the Vue console.
- Added interactive token recovery, sign-in/sign-out states, and authenticated operator/tenant display.
- Removed tenant IDs from mutation request contracts; tenant scope now comes only from the signed access token.
- Added `Integration.Operator` and `Integration.Admin` server authorization policies.
- Restricted connection creation and audit history to administrators and operational mutations to operators.
- Prevented authenticated API failures from falling back to representative demo data.
- Added Terraform inputs and build outputs for the SPA client, API audience, tenant, and scope.

## Iteration 7 — 2026-08-06: Asynchronous Connection Testing

- Added durable asynchronous connection-test requests and terminal result projections.
- Saved connection-test state, command outbox, and operator audit in one Control Plane transaction.
- Executed tests in the Worker through the real cached-secret and connector-construction path.
- Added a 30-second Worker timeout and sanitized credential, provider, network, and configuration failure categories.
- Added idempotent Worker inbox handling and result outbox publishing.
- Added Vue polling every two seconds, local timeout behavior, persisted latest-result reload, and premium status treatment.

## Iteration 6 — 2026-08-06: Microsoft Entra ID Authentication and Tenant Isolation

- Added Microsoft Entra ID bearer authentication with a secure fallback authorization policy.
- Derived tenant identity from a signed token claim instead of trusting query or request values.
- Enforced tenant filters across runs, record details, connections, reconciliation, and audit APIs.
- Added immutable operator audit records for connection creation and manual reconciliation.
- Added development-only authentication bypass with a fixed configured tenant.
- Added Terraform inputs and Container App settings for the API app registration.

## Iteration 5 — 2026-08-06: Key Vault Secret Resolution and Vault Routing

- Replaced opaque secret references in execution messages with structured vault, name, version, and routing fields.
- Added Azure Key Vault secret resolution using managed identity.
- Added a five-minute in-memory cache with concurrent request coalescing and a short failure cooldown.
- Added cache hit, miss, and refresh-failure metrics without tenant or secret values as dimensions.
- Added configurable tenant/region vault routing while retaining a shared-vault default.
- Changed Worker execution to construct one credentialed destination connector per run.
- Added a 5,000-request concurrency test that produces one underlying secret-provider call.
- Added masked credential status to the connection-management console.

## Iteration 4 — 2026-08-06: Capability-Aware Connectors and Unknown-Outcome Recovery

- Added tenant-scoped connection profiles with versioned capability snapshots on run commands.
- Distinguished confirmed no-commit failures from unknown outcomes and bounded only safe retries.
- Added read-after-write reconciliation for connectors that can prove an ambiguous write committed.
- Added Key Vault secret references without placing credentials in messages or configuration records.
- Granted the Worker managed identity least-privilege Key Vault secret access in Terraform.
- Added a connector registry and capability editor to the Vue operations console.
- Added a recovery test for ambiguous writes confirmed by destination lookup.

## Iteration 3 — 2026-08-06: Durable Worker Ledger and Record-Level Idempotency

- Added a Worker-owned Azure SQL database with a durable processed-record ledger, inbox, and outbox.
- Added a unique record delivery gate and canonical fingerprint to prevent duplicate destination writes.
- Added explicit `UnknownOutcome` handling; ambiguous writes stop and are never auto-retried.
- Added versioned record-result events, Control Plane projections, and reconciliation APIs.
- Added a separate Worker database to Terraform and a record-level operations console.
- Added a redelivery test proving the destination connector is not invoked twice.

## Iteration 2 — 2026-08-06: Control Plane–Worker Microservices and Azure Infrastructure

- Split the runtime into independently deployable Control Plane and Sync Worker services.
- Added versioned integration messages and a canonical time-entry contract.
- Replaced in-memory persistence with EF Core/Azure SQL configuration and an initial migration.
- Added tenant-scoped database idempotency and concurrent-duplicate recovery.
- Added a transactional outbox, Service Bus publisher, command worker, and result consumer.
- Added simulated field-operations and accounting connector boundaries.
- Moved CORS, SQL, and Service Bus settings into configuration.
- Added Terraform for private-networked Azure Container Apps, Azure SQL, Service Bus, identities, registry, Vue hosting, Key Vault, and observability.
- Added Dockerfiles, CI validation, ADRs, and Worker tests.

## Iteration 1 — 2026-08-06: Integration-Run Domain and Vue Operations Console

- Created the RelayWorks repository, integration-run domain, in-memory API slice, and Vue operations console.

