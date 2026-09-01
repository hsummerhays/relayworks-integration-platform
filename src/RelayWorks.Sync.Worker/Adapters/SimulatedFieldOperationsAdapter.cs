using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker.Adapters;

public sealed class SimulatedFieldOperationsAdapter : ITimeEntrySourceAdapter
{
    public string Provider => "SimulatedFieldOperations";

    public ConnectorCapabilities Capabilities { get; } = new(
        SupportsIdempotencyKey: true,
        SupportsReadAfterWrite: true,
        DefaultMaxConfirmedNoCommitRetries: 2);

    public IReadOnlyList<CanonicalTimeEntryV1> Read(
        TimeEntryReadRequest request,
        ConnectorContext context)
    {
        var results = new List<CanonicalTimeEntryV1>(request.TotalRecords);
        for (var index = 1; index <= request.TotalRecords; index++)
        {
            results.Add(new CanonicalTimeEntryV1(
                request.TenantId,
                $"time-{index:000000}",
                "1",
                $"employee-{(index % 12) + 1:000}",
                index % 10 == 0 ? "" : $"project-{(index % 4) + 1:000}",
                DateOnly.FromDateTime(request.OccurredAtUtc.UtcDateTime),
                8m,
                index % 7 == 0 ? 1m : 0m,
                "REG",
                request.RunId));
        }

        return results;
    }

    public async Task<ConnectorHealthResult> TestConnectionAsync(
        ConnectorContext context,
        CancellationToken cancellationToken)
    {
        if (context.Authenticator is not null)
        {
            var valid = await context.Authenticator.ValidateAsync(cancellationToken);
            if (!valid)
                return new(false, FailureCategory: "AuthenticationFailed", SafeMessage: "Field operations authentication failed.");
        }

        await Task.Delay(50, cancellationToken);
        return new(true, SafeMessage: "Field operations reachability confirmed.");
    }
}
