# ADR 0003: Manage Azure infrastructure with Terraform

## Status

Accepted for Iteration 2.

## Decision

Terraform owns RelayWorks Azure resources. Application builds, image publication, database migrations, and runtime data remain outside Terraform.

## Consequences

- Infrastructure changes are reviewable and repeatable.
- Remote state and CI authentication require an isolated bootstrap process (`infra/bootstrap/state`) with Entra ID OAuth auth and 30-day soft delete recovery protections.
- Personal development environments enforce explicit cost controls: $50/month Azure budget alerts, 0.1 GB/day Log Analytics ingestion quota, LRS storage replication, scale-to-zero compute with KEDA Service Bus scaling, and standardized tagging.
- Managed identities replace stored Azure service credentials.
- Terraform validation belongs in pull-request CI; apply remains approval-gated.
