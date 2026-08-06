using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RelayWorks.Contracts.Connections;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker.Persistence;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class ConnectionTestProcessorTests
{
    [Fact]
    public async Task Successful_test_is_saved_to_inbox_and_result_outbox()
    {
        var options = new DbContextOptionsBuilder<WorkerLedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WorkerLedgerDbContext(options);
        var processor = new ConnectionTestProcessor(new Factory(), db, TimeProvider.System,
            NullLogger<ConnectionTestProcessor>.Instance);
        var command = new ConnectionTestRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new ConnectorExecutionProfileV1("Test", true, true, 0, "v1",
                new SecretLocatorV1(new Uri("https://test.vault.azure.net"), "credential")), DateTimeOffset.UtcNow);

        await processor.ProcessAsync(command, default);

        Assert.True(await db.InboxMessages.AnyAsync(x => x.MessageId == command.MessageId));
        Assert.Equal(nameof(ConnectionTestCompletedV1), (await db.OutboxMessages.SingleAsync()).Type);
    }

    private sealed class Factory : ITimeEntryDestinationConnectorFactory
    {
        public Task<ITimeEntryDestinationConnector> CreateAsync(ConnectorExecutionProfileV1 profile,
            CancellationToken cancellationToken) => Task.FromResult<ITimeEntryDestinationConnector>(new Connector());
    }
    private sealed class Connector : ITimeEntryDestinationConnector
    {
        public DestinationWriteResult Write(CanonicalTimeEntryV1 entry, string idempotencyKey) => new(DestinationWriteStatus.Succeeded);
        public DestinationLookupResult FindByIdempotencyKey(string idempotencyKey) => new(false);
        public Task<ConnectorHealthResult> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorHealthResult(true, SafeMessage: "Connected."));
    }
}
