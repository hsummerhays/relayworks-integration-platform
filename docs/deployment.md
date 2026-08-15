# Deployment and rollback

CI must pass NuGet Audit during restore, .NET tests (including SQL Server Testcontainers and migrations), the Vue production build, and Terraform formatting/validation. High or critical advisories are corrected by upgrading or explicitly pinning a patched compatible dependency; they are not suppressed. Deployment uses Azure workload identity federation; long-lived client secrets are not required.

The deployment workflow creates a Terraform plan under a protected GitHub environment and preserves it as a short-lived immutable artifact. Applying infrastructure is intentionally a separate approval boundary. Container images should be addressed by digest, and database migration bundles should run from an approved job before application traffic moves to the new revision.

## Terraform state backend bootstrap

Remote state for environment modules is hosted in an Azure Blob Storage container (`tfstate`) within a dedicated state resource group (`rg-relayworks-tfstate`).

The state backend storage account is provisioned via `infra/bootstrap/state` and enforces authoritative recovery protections:
- Microsoft Entra ID authentication (`default_to_oauth_authentication = true`, `use_azuread_auth = true`) with shared access keys and local users disabled (`shared_access_key_enabled = false`, `local_user_enabled = false`).
- Blob versioning enabled (`versioning_enabled = true`).
- 30-day soft deletion retention for both individual blobs and containers.
- Private blob container access (`container_access_type = "private"`).

Environment deployments reference this state account using environment-specific `backend.hcl` files.

## Promotion order

1. Build, scan, and push immutable Control Plane and Worker images.
2. Produce and review the Terraform plan.
3. Back up databases and run EF migration bundles with managed identity.
4. Apply infrastructure and deploy a zero-traffic Container App revision.
5. Run `scripts/smoke-test.sh` against liveness and readiness endpoints.
6. Shift traffic to the Control Plane revision and then enable Worker consumption.
7. Observe exceptions, dead letters, outbox lag, and connector circuits before completing promotion.

Rollback application revisions before attempting schema rollback. EF down-migrations can destroy data and require a separate reviewed recovery plan. Additive database changes should remain compatible with the prior application revision for at least one deployment window. Archive deletion must remain in dry-run mode through initial deployment and one complete retention cycle.
