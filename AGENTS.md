# RelayWorks Platform Rules

## Verification & Test Suites

Before committing or pushing changes, verify all relevant subsystem test suites for the repository:

1. **.NET / C# Test Suite**:
   ```powershell
   dotnet test
   ```
2. **Vue Console Frontend**:
   ```powershell
   npm --prefix web/relayworks-console run build
   ```
3. **Terraform Infrastructure Validation**:
   ```powershell
   terraform -chdir=infra/environments/dev validate
   ```
4. **Service Bus E2E Tests** (when Service Bus emulator / DBs are running):
   ```powershell
   dotnet test tests/RelayWorks.ServiceBus.E2E.Tests/RelayWorks.ServiceBus.E2E.Tests.csproj
   ```

## Repository Specifics & Invariants

- Use the four verification suites above to satisfy the global pre-commit test gate.
- When running local DB or Service Bus dependent integration tests, ensure the local emulator container stack is up.

## Optional & Post-Deploy Verification

1. **Terraform Formatting & Bootstrap State**:
   ```powershell
   terraform -chdir=infra/environments/dev fmt -check
   terraform -chdir=infra/bootstrap/state validate
   ```
2. **Control Plane Health & Smoke Tests** (against live or local running container):
   ```bash
   CONTROL_PLANE_URL=http://localhost:5000 ./scripts/smoke-test.sh
   ```
