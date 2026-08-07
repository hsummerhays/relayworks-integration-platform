using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Persistence;
using RelayWorks.Sync.Worker.Resilience;

namespace RelayWorks.ServiceBus.E2E.Tests;

public sealed class ServiceBusWorkerE2ETests
{
    private const string CommandsQueue = "integration-commands";
    private const string EventsTopic = "integration-events";
    private const string ObserverSubscription = "e2e-observer";

    [Fact(Timeout = 120_000)]
    public async Task BrokerRedeliveryPreservesTheDurableDeliveryGate()
    {
        var busConnection = RequiredEnvironment("SERVICEBUS_EMULATOR_CONNECTION_STRING");
        var sqlConnection = RequiredEnvironment("SERVICEBUS_E2E_SQL_CONNECTION_STRING");
        await using var bus = new ServiceBusClient(busConnection);
        await DrainAsync(bus, ObserverSubscription, TestContext.Current.CancellationToken);

        var destination = new CountingDestination();
        var services = BuildServices(sqlConnection, destination);
        await using var provider = services.BuildServiceProvider();
        await ResetDatabaseAsync(provider, TestContext.Current.CancellationToken);

        var options = Options.Create(new ServiceBusOptions
        {
            ConnectionString = busConnection,
            CommandsQueue = CommandsQueue,
            EventsTopic = EventsTopic
        });
        await using var commandWorker = new IntegrationCommandWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), bus, options,
            TimeProvider.System, NullLogger<IntegrationCommandWorker>.Instance);
        var outboxPublisher = new WorkerOutboxPublisher(
            provider.GetRequiredService<IServiceScopeFactory>(), bus, options);

        await commandWorker.StartAsync(TestContext.Current.CancellationToken);
        await outboxPublisher.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var command = BuildCommand();
            await SendAsync(bus, command, TestContext.Current.CancellationToken);
            var firstEvents = await ReceiveEventsAsync(bus, 2, TestContext.Current.CancellationToken);

            Assert.Contains(nameof(IntegrationRecordResultsReportedV1), firstEvents);
            Assert.Contains(nameof(IntegrationRunCompletedV1), firstEvents);
            await AssertLedgerAsync(provider, expectedInbox: 1, expectedWrites: 1,
                destination, TestContext.Current.CancellationToken);

            // A broker redelivery retains the command MessageId even if the envelope is recreated.
            await SendAsync(bus, command, TestContext.Current.CancellationToken);
            // A new command identity for the same source record crosses the inbox gate and proves
            // the longer-lived record ledger also prevents a second destination write.
            await SendAsync(bus, command with { MessageId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);
            var replayEvents = await ReceiveEventsAsync(bus, 2, TestContext.Current.CancellationToken);

            Assert.Contains(nameof(IntegrationRecordResultsReportedV1), replayEvents);
            Assert.Contains(nameof(IntegrationRunCompletedV1), replayEvents);
            await AssertLedgerAsync(provider, expectedInbox: 2, expectedWrites: 1,
                destination, TestContext.Current.CancellationToken);
        }
        finally
        {
            await commandWorker.StopAsync(CancellationToken.None);
            await outboxPublisher.StopAsync(CancellationToken.None);
            outboxPublisher.Dispose();
        }
    }

    private static IServiceCollection BuildServices(string sqlConnection, CountingDestination destination)
    {
        var resilienceOptions = Options.Create(new ConnectorResilienceOptions
        {
            MaxConcurrentRequestsPerConnection = 2,
            RequestsPerSecondPerConnection = 100,
            BurstCapacityPerConnection = 100,
            BaseRetryDelayMilliseconds = 1,
            MaxRetryDelaySeconds = 1,
            CircuitFailureThreshold = 5,
            CircuitBreakSeconds = 1
        });
        var delay = new NoDelay();
        return new ServiceCollection()
            .AddLogging()
            .AddDbContext<WorkerLedgerDbContext>(options => options.UseSqlServer(sqlConnection))
            .AddScoped<ITimeEntrySourceConnector, SimulatedFieldOperationsConnector>()
            .AddSingleton<ITimeEntryDestinationConnectorFactory>(new FixedConnectorFactory(destination))
            .AddSingleton<IOptions<ConnectorResilienceOptions>>(resilienceOptions)
            .AddSingleton<IResilienceDelay>(delay)
            .AddSingleton<ConnectionExecutionGate>()
            .AddSingleton<DestinationResilienceExecutor>()
            .AddScoped<TimeEntryProcessor>();
    }

    private static async Task ResetDatabaseAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerLedgerDbContext>();
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    private static IntegrationRunRequestedV1 BuildCommand() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TimeEntryExport", 1,
        DateTimeOffset.UtcNow,
        new ConnectorExecutionProfileV1("E2E", true, false, 0, "e2e-v1",
            new SecretLocatorV1(new Uri("https://e2e.invalid"), "unused")));

    private static async Task SendAsync(ServiceBusClient bus, IntegrationRunRequestedV1 command,
        CancellationToken cancellationToken)
    {
        await using var sender = bus.CreateSender(CommandsQueue);
        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(command))
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = nameof(IntegrationRunRequestedV1),
            ContentType = "application/json",
            CorrelationId = command.RunId.ToString()
        };
        await sender.SendMessageAsync(message, cancellationToken);
    }

    private static async Task<HashSet<string>> ReceiveEventsAsync(ServiceBusClient bus, int count,
        CancellationToken cancellationToken)
    {
        await using var receiver = bus.CreateReceiver(EventsTopic, ObserverSubscription);
        var subjects = new HashSet<string>(StringComparer.Ordinal);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (subjects.Count < count && DateTimeOffset.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
            if (message is null) continue;
            subjects.Add(message.Subject);
            await receiver.CompleteMessageAsync(message, cancellationToken);
        }
        Assert.Equal(count, subjects.Count);
        return subjects;
    }

    private static async Task DrainAsync(ServiceBusClient bus, string subscription,
        CancellationToken cancellationToken)
    {
        await using var receiver = bus.CreateReceiver(EventsTopic, subscription,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        while (await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(250), cancellationToken) is not null) { }
    }

    private static async Task AssertLedgerAsync(IServiceProvider provider, int expectedInbox,
        int expectedWrites, CountingDestination destination, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerLedgerDbContext>();
        Assert.Equal(expectedInbox, await db.InboxMessages.CountAsync(cancellationToken));
        Assert.Single(await db.RecordDeliveries.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Equal(expectedWrites, destination.Writes);
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required E2E setting '{name}' was not provided.");

    private sealed class FixedConnectorFactory(ITimeEntryDestinationConnector connector)
        : ITimeEntryDestinationConnectorFactory
    {
        public Task<ITimeEntryDestinationConnector> CreateAsync(ConnectorExecutionProfileV1 profile,
            CancellationToken cancellationToken) => Task.FromResult(connector);
    }

    private sealed class CountingDestination : ITimeEntryDestinationConnector
    {
        private int _writes;
        public int Writes => Volatile.Read(ref _writes);

        public Task<DestinationWriteResult> WriteAsync(
            RelayWorks.Contracts.TimeEntries.CanonicalTimeEntryV1 entry, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _writes);
            return Task.FromResult(new DestinationWriteResult(DestinationWriteStatus.Succeeded,
                $"e2e:{entry.SourceRecordId}"));
        }

        public Task<DestinationLookupResult> FindByIdempotencyKeyAsync(string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult(new DestinationLookupResult(false));

        public Task<ConnectorHealthResult> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorHealthResult(true));
    }

    private sealed class NoDelay : IResilienceDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
