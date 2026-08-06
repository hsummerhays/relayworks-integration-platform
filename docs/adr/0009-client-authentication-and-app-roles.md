# ADR 0009: Client authentication and server-enforced app roles

## Status

Accepted — 2026-08-06

## Decision

The Vue single-page application authenticates through MSAL and requests the Control Plane delegated API scope. API requests use silently acquired bearer tokens; an interactive popup is used only when Entra requires user interaction. Session tokens use browser session storage.

Tenant identity is read from the `relayworks_tenant_id` access-token claim. Mutation payloads no longer contain tenant IDs. The API applies two app roles: `Integration.Operator` for run submission, connection testing, and reconciliation; `Integration.Admin` additionally permits connection creation and audit access. UI visibility is convenience only—the API policies are authoritative.

## Consequences

- The SPA and API require separate Entra app registrations and a delegated `access_as_user` scope.
- User or group assignments must supply an app role and a valid RelayWorks tenant claim.
- A missing tenant claim produces no application session.
- Authentication failures never activate demo data in an authenticated deployment.
- Terraform exports Vite build settings; the CI build injects them because Vite variables are compile-time values.
