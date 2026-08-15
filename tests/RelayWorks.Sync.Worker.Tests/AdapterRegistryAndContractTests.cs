using Microsoft.Extensions.DependencyInjection;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker.Adapters;
using RelayWorks.Sync.Worker.Authentication;
using RelayWorks.Sync.Worker.Connectors;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class AdapterRegistryAndContractTests
{
    private static ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeEntrySourceAdapter, SimulatedFieldOperationsAdapter>();
        services.AddSingleton<ITimeEntryDestinationAdapter, SimulatedAccountingAdapter>();
        services.AddSingleton<ITimeEntryDestinationAdapter, FieldFloAccountingAdapter>();
        services.AddSingleton<IAdapterRegistry, AdapterRegistry>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Registry_resolves_registered_destination_adapter()
    {
        var provider = BuildTestServiceProvider();
        var registry = provider.GetRequiredService<IAdapterRegistry>();

        var accounting = registry.GetDestinationAdapter("SimulatedAccounting");
        Assert.NotNull(accounting);
        Assert.Equal("SimulatedAccounting", accounting.Provider);

        var fieldFlo = registry.GetDestinationAdapter("FieldFloAccounting");
        Assert.NotNull(fieldFlo);
        Assert.Equal("FieldFloAccounting", fieldFlo.Provider);
    }

    [Fact]
    public void Registry_throws_informative_exception_for_unknown_provider()
    {
        var registry = new AdapterRegistry([], []);
        var ex = Assert.Throws<NotSupportedException>(() => registry.GetDestinationAdapter("QuickBooksDesktop"));
        Assert.Contains("QuickBooksDesktop", ex.Message);
    }

    [Fact]
    public void Source_adapter_reads_canonical_time_entries_through_registry()
    {
        var provider = BuildTestServiceProvider();
        var registry = provider.GetRequiredService<IAdapterRegistry>();
        var source = registry.GetSourceAdapter("FieldFlo");

        var request = new TimeEntryReadRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, DateTimeOffset.UtcNow);
        var context = new ConnectorContext(request.TenantId, request.ConnectionId, "v1", source.Capabilities, new ApiKeyAuthenticator("key"));

        var entries = source.Read(request, context);

        Assert.Equal(5, entries.Count);
        Assert.All(entries, e =>
        {
            Assert.Equal(request.TenantId, e.TenantId);
            Assert.Equal(request.RunId, e.CorrelationId);
            Assert.NotEmpty(e.SourceRecordId);
        });
    }

    [Theory]
    [MemberData(nameof(GetRegisteredAdaptersFromDi))]
    public async Task Every_registered_adapter_satisfies_successful_write_and_read_after_write_contract(ITimeEntryDestinationAdapter adapter)
    {
        var tenantId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var context = new ConnectorContext(tenantId, Guid.NewGuid(), "v1", adapter.Capabilities, new ApiKeyAuthenticator("key"));

        var entry = new CanonicalTimeEntryV1(tenantId, $"time-{Guid.NewGuid():N}", "1", "emp-001", "proj-001", DateOnly.FromDateTime(DateTime.UtcNow), 8m, 0m, "REG", runId);
        var writeResult = await adapter.WriteAsync(entry, $"idempotency-{entry.SourceRecordId}", context, TestContext.Current.CancellationToken);

        Assert.Equal(DestinationWriteStatus.Succeeded, writeResult.Status);
        Assert.NotNull(writeResult.DestinationReference);

        if (adapter.Capabilities.SupportsReadAfterWrite)
        {
            var lookup = await adapter.FindExistingAsync($"idempotency-{entry.SourceRecordId}", context, TestContext.Current.CancellationToken);
            Assert.True(lookup.Found);
            Assert.Equal(writeResult.DestinationReference, lookup.DestinationReference);
        }
    }

    [Theory]
    [MemberData(nameof(GetRegisteredAdaptersFromDi))]
    public async Task Every_registered_adapter_satisfies_rejection_contract_for_invalid_record(ITimeEntryDestinationAdapter adapter)
    {
        var tenantId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var context = new ConnectorContext(tenantId, Guid.NewGuid(), "v1", adapter.Capabilities, new ApiKeyAuthenticator("key"));

        // Missing employee reference
        var entry = new CanonicalTimeEntryV1(tenantId, $"time-{Guid.NewGuid():N}", "1", "", "proj-001", DateOnly.FromDateTime(DateTime.UtcNow), 8m, 0m, "REG", runId);
        var writeResult = await adapter.WriteAsync(entry, $"idempotency-{entry.SourceRecordId}", context, TestContext.Current.CancellationToken);

        Assert.Equal(DestinationWriteStatus.Rejected, writeResult.Status);
        Assert.Equal("EMPLOYEE_REQUIRED", writeResult.ErrorCode);
    }

    [Fact]
    public void Factory_creation_fails_when_profile_claims_unsupported_adapter_capabilities()
    {
        var adapter = new SimulatedAccountingAdapter();
        var registry = new AdapterRegistry([], [adapter]);
        var factory = new TimeEntryDestinationConnectorFactory(registry, new MockAuthenticatorFactory(), Microsoft.Extensions.Logging.Abstractions.NullLogger<TimeEntryDestinationConnectorFactory>.Instance);

        // Claiming retry count exceeding adapter capacity (e.g. 10 > 2)
        var invalidProfile = new ConnectorExecutionProfileV1("SimulatedAccounting", true, true, 10, "v1",
            new SecretLocatorV1(new Uri("https://vault.azure.net"), "secret"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateAsync(invalidProfile, Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Registry_fails_deterministically_on_duplicate_destination_registration()
    {
        var adapter1 = new SimulatedAccountingAdapter();
        var adapter2 = new SimulatedAccountingAdapter();

        var ex = Assert.Throws<InvalidOperationException>(() => new AdapterRegistry([], [adapter1, adapter2]));
        Assert.Contains("Duplicate destination adapter registration", ex.Message);
        Assert.Contains("SimulatedAccounting", ex.Message);
    }

    [Fact]
    public void Registry_fails_deterministically_on_duplicate_source_registration()
    {
        var adapter1 = new SimulatedFieldOperationsAdapter();
        var adapter2 = new SimulatedFieldOperationsAdapter();

        var ex = Assert.Throws<InvalidOperationException>(() => new AdapterRegistry([adapter1, adapter2], []));
        Assert.Contains("Duplicate source adapter registration", ex.Message);
        Assert.Contains("FieldFlo", ex.Message);
    }

    public static TheoryData<ITimeEntryDestinationAdapter> GetRegisteredAdaptersFromDi()
    {
        var provider = BuildTestServiceProvider();
        var adapters = provider.GetServices<ITimeEntryDestinationAdapter>();
        var data = new TheoryData<ITimeEntryDestinationAdapter>();
        foreach (var adapter in adapters)
        {
            data.Add(adapter);
        }
        return data;
    }

    private sealed class MockAuthenticatorFactory : IConnectorAuthenticatorFactory
    {
        public Task<IConnectorAuthenticator> CreateAuthenticatorAsync(ConnectorExecutionProfileV1 profile, CancellationToken cancellationToken) =>
            Task.FromResult<IConnectorAuthenticator>(new ApiKeyAuthenticator("test"));
    }
}
