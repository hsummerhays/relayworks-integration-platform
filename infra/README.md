# RelayWorks infrastructure

Terraform provisions the Azure development environment for the Control Plane and Sync Worker services.

## State bootstrap

The state-storage stack intentionally uses local state because it creates the remote backend used by the main environment. Run it once with an authorized Azure identity, then grant the CI identity `Storage Blob Data Contributor` on the state account.

## Development environment

```bash
cd infra/environments/dev
cp backend.hcl.example backend.hcl
cp terraform.tfvars.example terraform.tfvars
terraform init -backend-config=backend.hcl
terraform plan
```

No state, plan, backend configuration, or populated variable file belongs in source control.

## Identity and database bootstrap

Container Apps use user-assigned managed identities for Azure Container Registry, Service Bus, and Azure SQL authentication. Terraform creates the Entra-only SQL server and private endpoint. After the database exists, an Entra administrator must create the Control Plane identity as a contained database user and grant only the permissions needed to run the application. EF migrations should run from an approved deployment job with network access to the private endpoint; Terraform does not execute migrations.

## Current scope

Implemented in Terraform:

- private-networked Container Apps environment;
- Control Plane and Sync Worker apps;
- Azure Static Web Apps host for the Vue console (content deployment remains in CI);
- Azure Container Registry;
- Azure SQL server and control-plane database;
- Service Bus command queue, event topic, and Control Plane subscription;
- managed identities and least-privilege Service Bus roles;
- Key Vault, Log Analytics, and Application Insights.

GitHub workload-identity federation is documented but not provisioned because its subject depends on the final GitHub organization and repository name.
