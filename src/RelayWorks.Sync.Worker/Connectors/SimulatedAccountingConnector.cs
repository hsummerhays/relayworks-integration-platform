using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public sealed class SimulatedAccountingConnector : ITimeEntryDestinationConnector
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Committed = [];

    public DestinationWriteResult Write(CanonicalTimeEntryV1 entry, string idempotencyKey)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(entry.EmployeeReference)) errors.Add("EMPLOYEE_REQUIRED");
        if (string.IsNullOrWhiteSpace(entry.ProjectReference)) errors.Add("PROJECT_REQUIRED");
        if (entry.RegularHours < 0 || entry.OvertimeHours < 0) errors.Add("HOURS_NEGATIVE");
        if (entry.RegularHours + entry.OvertimeHours > 24) errors.Add("HOURS_EXCEED_DAY");
        if (string.IsNullOrWhiteSpace(entry.LaborCode)) errors.Add("LABOR_CODE_REQUIRED");
        if (errors.Count > 0)
            return new(DestinationWriteStatus.Rejected, ErrorCode: errors[0], ErrorMessage: string.Join(", ", errors));

        // The simulator makes explicit what a real connector must report: a timeout after
        // submission is not a failure and must not be retried without reconciliation.
        if (entry.SourceRecordId.EndsWith("000013", StringComparison.Ordinal))
            return new(DestinationWriteStatus.ConfirmedNoCommit, ErrorCode: "DESTINATION_UNAVAILABLE",
                ErrorMessage: "The destination confirmed that no write was committed.");

        if (entry.SourceRecordId.EndsWith("000017", StringComparison.Ordinal))
        {
            var ambiguousReference = $"acct:{entry.TenantId:N}:{entry.SourceRecordId}:{entry.SourceVersion}";
            Committed[idempotencyKey] = ambiguousReference;
            return new(DestinationWriteStatus.UnknownOutcome, ErrorCode: "DESTINATION_TIMEOUT",
                ErrorMessage: "The destination did not confirm whether the write committed.");
        }

        var reference = $"acct:{entry.TenantId:N}:{entry.SourceRecordId}:{entry.SourceVersion}";
        Committed[idempotencyKey] = reference;
        return new(DestinationWriteStatus.Succeeded, reference);
    }

    public DestinationLookupResult FindByIdempotencyKey(string idempotencyKey) =>
        Committed.TryGetValue(idempotencyKey, out var reference)
            ? new(true, reference)
            : new(false);

    public async Task<ConnectorHealthResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(150, cancellationToken);
        return new(true, SafeMessage: "Authentication and provider reachability confirmed.");
    }
}
