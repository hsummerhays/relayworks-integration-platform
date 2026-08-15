using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker.Adapters;
using RelayWorks.Sync.Worker.Authentication;

namespace RelayWorks.Sync.Worker.Connectors;

public sealed class RegistryTimeEntrySourceConnector(
    IAdapterRegistry adapterRegistry) : ITimeEntrySourceConnector
{
    public IEnumerable<CanonicalTimeEntryV1> Read(IntegrationRunRequestedV1 command)
    {
        var profile = command.ConnectorProfile ?? throw new InvalidOperationException("Connector profile is required.");
        
        // Resolve the source adapter from the dynamic registry (defaulting to FieldFlo for time entry exports if unspecified)
        var provider = string.IsNullOrWhiteSpace(profile.Provider) ? "FieldFlo" : profile.Provider;
        if (!adapterRegistry.TryGetSourceAdapter(provider, out var adapter) || adapter is null)
        {
            // If the execution profile provider is a destination provider (e.g. SimulatedAccounting), resolve the default source adapter
            adapter = adapterRegistry.GetSourceAdapter("FieldFlo");
        }

        var capabilities = new ConnectorCapabilities(
            profile.SupportsIdempotencyKey,
            profile.SupportsReadAfterWrite,
            profile.MaxConfirmedNoCommitRetries);

        var context = new ConnectorContext(
            command.TenantId,
            command.ConnectionId,
            profile.ConfigurationVersion,
            capabilities,
            new ApiKeyAuthenticator("source-internal"));

        var readRequest = new TimeEntryReadRequest(
            command.TenantId,
            command.ConnectionId,
            command.RunId,
            command.TotalRecords,
            command.OccurredAtUtc);

        return adapter.Read(readRequest, context);
    }
}
