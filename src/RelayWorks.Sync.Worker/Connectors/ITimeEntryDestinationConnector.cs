using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntryDestinationConnector
{
    Task<DestinationWriteResult> WriteAsync(CanonicalTimeEntryV1 entry, string idempotencyKey,
        CancellationToken cancellationToken);
    Task<DestinationLookupResult> FindByIdempotencyKeyAsync(string idempotencyKey,
        CancellationToken cancellationToken);
    Task<ConnectorHealthResult> TestConnectionAsync(CancellationToken cancellationToken);
}

public enum DestinationWriteStatus { Succeeded, Rejected, ConfirmedNoCommit, UnknownOutcome }

public sealed record DestinationWriteResult(
    DestinationWriteStatus Status,
    string? DestinationReference = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    TimeSpan? RetryAfter = null);

public sealed record DestinationLookupResult(bool Found, string? DestinationReference = null);
public sealed record ConnectorHealthResult(bool Succeeded, string? FailureCategory = null, string? SafeMessage = null);
