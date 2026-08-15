using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker.Adapters;
using RelayWorks.Sync.Worker.Authentication;

namespace RelayWorks.Sync.Worker;

public sealed partial class TimeEntryDestinationConnectorFactory(
    IAdapterRegistry adapterRegistry,
    IConnectorAuthenticatorFactory authenticatorFactory,
    ILogger<TimeEntryDestinationConnectorFactory> logger) : ITimeEntryDestinationConnectorFactory
{
    public async Task<ITimeEntryDestinationConnector> CreateAsync(
        ConnectorExecutionProfileV1 profile,
        Guid tenantId,
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var adapter = adapterRegistry.GetDestinationAdapter(profile.Provider);

        // Validate snapshotted profile capabilities against the adapter's declared capabilities
        ValidateProfileCapabilities(profile, adapter);

        var authenticator = await authenticatorFactory.CreateAuthenticatorAsync(profile, cancellationToken);

        var effectiveCapabilities = new ConnectorCapabilities(
            profile.SupportsIdempotencyKey,
            profile.SupportsReadAfterWrite,
            profile.MaxConfirmedNoCommitRetries);

        var context = new ConnectorContext(
            tenantId,
            connectionId,
            profile.ConfigurationVersion,
            effectiveCapabilities,
            authenticator);

        LogConnectorCreated(logger, profile.Provider, profile.AuthType, profile.Secret.SecretVersion ?? "latest");

        return new AdapterConnectorWrapper(adapter, context);
    }

    public Task<ITimeEntryDestinationConnector> CreateAsync(
        ConnectorExecutionProfileV1 profile,
        CancellationToken cancellationToken) =>
        CreateAsync(profile, Guid.Empty, Guid.Empty, cancellationToken);

    private static void ValidateProfileCapabilities(ConnectorExecutionProfileV1 profile, ITimeEntryDestinationAdapter adapter)
    {
        if (profile.SupportsReadAfterWrite && !adapter.Capabilities.SupportsReadAfterWrite)
        {
            throw new InvalidOperationException(
                $"Connection profile claims read-after-write support, but provider adapter '{adapter.Provider}' does not support read-after-write.");
        }

        if (profile.SupportsIdempotencyKey && !adapter.Capabilities.SupportsIdempotencyKey)
        {
            throw new InvalidOperationException(
                $"Connection profile claims idempotency-key support, but provider adapter '{adapter.Provider}' does not support idempotency keys.");
        }

        if (profile.MaxConfirmedNoCommitRetries > adapter.Capabilities.DefaultMaxConfirmedNoCommitRetries)
        {
            throw new InvalidOperationException(
                $"Connection profile claims {profile.MaxConfirmedNoCommitRetries} max retries, exceeding adapter '{adapter.Provider}' maximum capability ({adapter.Capabilities.DefaultMaxConfirmedNoCommitRetries}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {Provider} connector adapter using {AuthType} and secret version {SecretVersion}")]
    private static partial void LogConnectorCreated(ILogger logger, string provider, ConnectorAuthenticationType authType, string secretVersion);

    private sealed class AdapterConnectorWrapper(
        ITimeEntryDestinationAdapter adapter,
        ConnectorContext context) : ITimeEntryDestinationConnector
    {
        public Task<DestinationWriteResult> WriteAsync(
            CanonicalTimeEntryV1 entry,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            adapter.WriteAsync(entry, idempotencyKey, context, cancellationToken);

        public Task<DestinationLookupResult> FindByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            adapter.FindExistingAsync(idempotencyKey, context, cancellationToken);

        public Task<ConnectorHealthResult> TestConnectionAsync(
            CancellationToken cancellationToken) =>
            adapter.TestConnectionAsync(context, cancellationToken);
    }
}
