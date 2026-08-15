using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker.Adapters;

public sealed record ConnectorCapabilities(
    bool SupportsIdempotencyKey,
    bool SupportsReadAfterWrite,
    int DefaultMaxConfirmedNoCommitRetries = 2);

public sealed record ConnectorContext(
    Guid TenantId,
    Guid ConnectionId,
    string ConfigurationVersion,
    ConnectorCapabilities Capabilities,
    Authentication.IConnectorAuthenticator Authenticator);

public sealed record TimeEntryReadRequest(
    Guid TenantId,
    Guid ConnectionId,
    Guid RunId,
    int TotalRecords,
    DateTimeOffset OccurredAtUtc);

public interface IIntegrationAdapter
{
    string Provider { get; }
    ConnectorCapabilities Capabilities { get; }

    Task<ConnectorHealthResult> TestConnectionAsync(
        ConnectorContext context,
        CancellationToken cancellationToken);
}

public interface ITimeEntrySourceAdapter : IIntegrationAdapter
{
    IReadOnlyList<CanonicalTimeEntryV1> Read(
        TimeEntryReadRequest request,
        ConnectorContext context);
}

public interface ITimeEntryDestinationAdapter : IIntegrationAdapter
{
    Task<DestinationWriteResult> WriteAsync(
        CanonicalTimeEntryV1 entry,
        string idempotencyKey,
        ConnectorContext context,
        CancellationToken cancellationToken);

    Task<DestinationLookupResult> FindExistingAsync(
        string idempotencyKey,
        ConnectorContext context,
        CancellationToken cancellationToken);
}
