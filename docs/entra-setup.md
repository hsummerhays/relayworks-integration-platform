# Microsoft Entra setup

RelayWorks expects two app registrations:

1. **Control Plane API** — exposes `api://<api-client-id>/access_as_user`, defines `Integration.Operator` and `Integration.Admin`, and emits `relayworks_tenant_id` in access tokens through the organization's approved claims policy.
2. **Vue console SPA** — uses the Static Web App URL as its SPA redirect URI and has delegated permission to `access_as_user`.

Assign users or groups to the least-privileged app role. The tenant claim must contain the RelayWorks application tenant GUID, not the Entra directory tenant ID. CI should consume Terraform's `console_auth_build_settings` output when building the Vite application. Local development keeps authentication disabled and uses the explicit development tenant configured by the API and Vite environment.
