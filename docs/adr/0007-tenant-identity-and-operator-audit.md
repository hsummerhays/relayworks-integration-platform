# ADR 0007: Token-derived tenant identity and operator audit

## Status

Accepted — 2026-08-06

## Decision

Production Control Plane endpoints require Microsoft Entra ID bearer authentication. Tenant scope comes exclusively from the signed `relayworks_tenant_id` claim; caller-supplied tenant identifiers are validated but never trusted as authorization evidence. Cross-tenant record lookups return no data.

Operator actions that change connection or reconciliation state append an immutable audit record containing tenant, Entra object id, action, resource identity, detail, and timestamp. Development may disable authentication only with an explicit fixed tenant setting.

Connection testing remains an asynchronous Worker concern and will not be represented by a synchronous Control Plane endpoint that merely validates configuration.
