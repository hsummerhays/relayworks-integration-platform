using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntryDestinationConnectorFactory
{
    Task<ITimeEntryDestinationConnector> CreateAsync(
        ConnectorExecutionProfileV1 profile,
        CancellationToken cancellationToken);
}
