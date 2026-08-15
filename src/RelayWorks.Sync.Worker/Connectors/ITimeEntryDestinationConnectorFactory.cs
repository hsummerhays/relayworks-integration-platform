using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntryDestinationConnectorFactory
{
    Task<ITimeEntryDestinationConnector> CreateAsync(
        ConnectorExecutionProfileV1 profile,
        Guid tenantId,
        Guid connectionId,
        CancellationToken cancellationToken);

    Task<ITimeEntryDestinationConnector> CreateAsync(
        ConnectorExecutionProfileV1 profile,
        CancellationToken cancellationToken) =>
        CreateAsync(profile, Guid.Empty, Guid.Empty, cancellationToken);
}
