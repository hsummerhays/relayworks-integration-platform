# ADR 0003: Manage Azure infrastructure with Terraform

## Status

Accepted for Iteration 2.

## Decision

Terraform owns RelayWorks Azure resources. Application builds, image publication, database migrations, and runtime data remain outside Terraform.

## Consequences

- Infrastructure changes are reviewable and repeatable.
- Remote state and CI authentication require a bootstrap process.
- Managed identities replace stored Azure service credentials.
- Terraform validation belongs in pull-request CI; apply remains approval-gated.
