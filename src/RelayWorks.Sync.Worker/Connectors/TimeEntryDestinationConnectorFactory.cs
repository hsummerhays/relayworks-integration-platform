using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker.Secrets;

namespace RelayWorks.Sync.Worker;

public sealed class TimeEntryDestinationConnectorFactory(
    CachedSecretResolver secretResolver,
    ILogger<TimeEntryDestinationConnectorFactory> logger) : ITimeEntryDestinationConnectorFactory
{
    public async Task<ITimeEntryDestinationConnector> CreateAsync(
        ConnectorExecutionProfileV1 profile,
        CancellationToken cancellationToken)
    {
        var credential = await secretResolver.ResolveAsync(profile.Secret, cancellationToken);
        logger.LogInformation("Created {Provider} connector using secret version {SecretVersion}",
            profile.Provider, profile.Secret.SecretVersion ?? "latest");

        // The reference adapter deliberately consumes but never stores or logs the credential.
        // A production provider factory would pass it to its authenticated SDK client here.
        _ = credential.Length;
        return profile.Provider switch
        {
            "SimulatedAccounting" or "FieldFloAccounting" => new SimulatedAccountingConnector(),
            _ => throw new NotSupportedException($"Connector provider '{profile.Provider}' is not registered.")
        };
    }
}
