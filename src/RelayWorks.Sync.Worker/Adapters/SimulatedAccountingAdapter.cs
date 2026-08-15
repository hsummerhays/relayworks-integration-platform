using System.Collections.Concurrent;
using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker.Adapters;

public sealed class SimulatedAccountingAdapter : ITimeEntryDestinationAdapter
{
    private static readonly ConcurrentDictionary<string, string> Committed = new(StringComparer.Ordinal);

    public string Provider => "SimulatedAccounting";

    public ConnectorCapabilities Capabilities { get; } = new(
        SupportsIdempotencyKey: true,
        SupportsReadAfterWrite: true,
        DefaultMaxConfirmedNoCommitRetries: 2);

    public Task<DestinationWriteResult> WriteAsync(
        CanonicalTimeEntryV1 entry,
        string idempotencyKey,
        ConnectorContext context,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(entry.EmployeeReference)) errors.Add("EMPLOYEE_REQUIRED");
        if (string.IsNullOrWhiteSpace(entry.ProjectReference)) errors.Add("PROJECT_REQUIRED");
        if (entry.RegularHours < 0 || entry.OvertimeHours < 0) errors.Add("HOURS_NEGATIVE");
        if (entry.RegularHours + entry.OvertimeHours > 24) errors.Add("HOURS_EXCEED_DAY");
        if (string.IsNullOrWhiteSpace(entry.LaborCode)) errors.Add("LABOR_CODE_REQUIRED");
        if (errors.Count > 0)
            return Task.FromResult(new DestinationWriteResult(DestinationWriteStatus.Rejected,
                ErrorCode: errors[0], ErrorMessage: string.Join(", ", errors)));

        if (entry.SourceRecordId.EndsWith("000013", StringComparison.Ordinal))
            return Task.FromResult(new DestinationWriteResult(DestinationWriteStatus.ConfirmedNoCommit,
                ErrorCode: "DESTINATION_RATE_LIMITED",
                ErrorMessage: "The destination declined the request before committing it.",
                RetryAfter: TimeSpan.FromMilliseconds(25)));

        if (entry.SourceRecordId.EndsWith("000017", StringComparison.Ordinal))
        {
            var ambiguousReference = $"acct:{entry.TenantId:N}:{entry.SourceRecordId}:{entry.SourceVersion}";
            Committed[idempotencyKey] = ambiguousReference;
            return Task.FromResult(new DestinationWriteResult(DestinationWriteStatus.UnknownOutcome,
                ErrorCode: "DESTINATION_TIMEOUT",
                ErrorMessage: "The destination did not confirm whether the write committed."));
        }

        var reference = $"acct:{entry.TenantId:N}:{entry.SourceRecordId}:{entry.SourceVersion}";
        Committed[idempotencyKey] = reference;
        return Task.FromResult(new DestinationWriteResult(DestinationWriteStatus.Succeeded, reference));
    }

    public Task<DestinationLookupResult> FindExistingAsync(
        string idempotencyKey,
        ConnectorContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Committed.TryGetValue(idempotencyKey, out var reference)
            ? new DestinationLookupResult(true, reference)
            : new DestinationLookupResult(false));

    public async Task<ConnectorHealthResult> TestConnectionAsync(
        ConnectorContext context,
        CancellationToken cancellationToken)
    {
        if (context.Authenticator is not null)
        {
            var valid = await context.Authenticator.ValidateAsync(cancellationToken);
            if (!valid)
                return new(false, FailureCategory: "AuthenticationFailed", SafeMessage: "Connector authentication strategy validation failed.");

            // Wire up HttpClient handler configuration (e.g. attaching client certificate for mTLS)
            using var clientHandler = new HttpClientHandler();
            using var testClient = new HttpClient(clientHandler);
            context.Authenticator.ConfigureClient(testClient, clientHandler);
        }

        await Task.Delay(150, cancellationToken);
        return new(true, SafeMessage: "Authentication and provider reachability confirmed.");
    }
}
