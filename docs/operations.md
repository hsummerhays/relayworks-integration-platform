# RelayWorks operations runbook

## Signals and ownership

| Signal | Meaning | First response |
| --- | --- | --- |
| `/health` fails | Control Plane process is unavailable | Inspect the active Container App revision and recent exceptions. |
| `/health/ready` reports `control-database` | SQL is unreachable | Check private DNS, SQL availability, managed identity, and firewall configuration. |
| `/health/ready` reports `command-outbox` | Commands have remained unpublished for over 60 seconds | Inspect the Control Plane publisher and Service Bus availability; do not manually replay until the durable rows are understood. |
| Service Bus dead-letter alert | A command or result exhausted normal delivery | Inspect dead-letter reason and message type, then compare the Worker inbox/ledger before resubmission. |
| Application exception alert | One or both services emitted an unhandled exception | Use operation and correlation fields to identify the affected run or test. |
| `UnknownOutcome` record | A connector write may have committed | Verify at the destination and use the reconciliation workflow; never blind-retry. |
| Connector circuit opens | Repeated calls were confirmed not committed | Check provider health and rate-limit guidance; the connection is paused briefly to prevent a retry storm. |

The alert email is a Terraform input. Production should use a monitored distribution list or incident-management receiver rather than an individual mailbox.

## Destination throttling

The Worker shares limits by connection-profile ID, so simultaneous runs on the active replica cannot independently overwhelm the same ERP. Defaults are two concurrent calls, five sustained calls per second, and a five-call burst. Tune these against the provider contract and the customer's environment; an older on-premises endpoint may require substantially lower values. Terraform intentionally caps the Worker at one replica until the token and circuit state have distributed coordination.

A connector may translate HTTP 429 into `ConfirmedNoCommit` only when the provider contract guarantees the request was rejected before processing. It should copy a valid `Retry-After` value into the connector result. A timeout after sending request content remains `UnknownOutcome`, regardless of HTTP retry conventions.

Current backoff is intentionally bounded to short Worker waits. Long provider maintenance windows require durable scheduled redelivery rather than holding a Worker execution open.

## Archival and retention

Archival starts in dry-run mode. Review `relayworks.archive.candidates` for at least one full retention cycle before setting `Archive__DryRun=false`. An eligible run must be terminal, older than 30 days, and free of unresolved `Rejected` or `UnknownOutcome` records.

For every archived run, confirm that both `<run>.json.gz` and `<run>.manifest.json` exist under the tenant/year/month partition. The manifest records schema version, row count, compressed length, SHA-256, and archive time. An `IntegrationRunArchived` system audit entry preserves the run ID and blob path after SQL removal.

If an archive cycle fails before deletion, leave the SQL rows in place and rerun it; the deterministic blob path and storage versioning make replacement recoverable. If verification fails, do not manually delete SQL data. Investigate storage integrity and identity permissions first.

Worker inbox and outbox cleanup never touches the delivery ledger. Unprocessed outbox rows, active runs, unresolved records, and delivery tombstones are outside automated deletion.

## Application Insights queries

The workspace-based Application Insights resource stores OpenTelemetry data in the `App*` tables. Adjust the time range before incident review.

### Trace a run or connection test

```kusto
let correlationId = "<run-or-test-guid>";
AppTraces
| where TimeGenerated > ago(24h)
| where tostring(Properties["relayworks.business_correlation_id"]) == correlationId
   or tostring(Properties["relayworks.run_id"]) == correlationId
   or tostring(Properties["relayworks.test_id"]) == correlationId
| project TimeGenerated, AppRoleName, SeverityLevel, Message, OperationId, Properties
| order by TimeGenerated asc
```

### Slow or unsuccessful connector calls

```kusto
AppDependencies
| where TimeGenerated > ago(1h)
| where Name startswith "connector "
| summarize Calls=count(), Failures=countif(Success == false),
    P95=percentile(DurationMs, 95) by AppRoleName, Name, bin(TimeGenerated, 5m)
| order by TimeGenerated desc
```

### Recent exceptions by service

```kusto
AppExceptions
| where TimeGenerated > ago(1h)
| summarize Count=count(), Samples=make_set(OuterMessage, 3)
    by AppRoleName, ProblemId, bin(TimeGenerated, 5m)
| order by TimeGenerated desc
```

### Service Bus processing spans

```kusto
AppDependencies
| where TimeGenerated > ago(1h)
| where DependencyType == "InProc" or Target has "servicebus"
| project TimeGenerated, AppRoleName, Name, Success, DurationMs, OperationId, Properties
| order by TimeGenerated desc
```

## Safe replay checklist

1. Identify the message ID, message type, run/test ID, and dead-letter reason.
2. Confirm whether the Worker inbox marks the command complete.
3. For integration commands, inspect every relevant delivery-ledger row.
4. If any row is `Processing` after a crash or `UnknownOutcome`, verify the destination before changing state.
5. Prefer repairing the underlying fault and redelivering the original message. Preserve its stable message identity and correlation context.
6. Record any manual reconciliation in the Control Plane so the operator audit remains complete.
