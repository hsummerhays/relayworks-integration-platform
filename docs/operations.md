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

The alert email is a Terraform input. Production should use a monitored distribution list or incident-management receiver rather than an individual mailbox.

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
