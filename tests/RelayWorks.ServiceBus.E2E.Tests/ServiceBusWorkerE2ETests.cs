using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using RelayWorks.Application.Abstractions;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.IntegrationRuns;
using RelayWorks.Infrastructure.Messaging;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Adapters;
using RelayWorks.Sync.Worker.Authentication;
using RelayWorks.Sync.Worker.Connectors;
using RelayWorks.Sync.Worker.Persistence;
using RelayWorks.Sync.Worker.Resilience;
using ControlPlaneServiceBusOptions = RelayWorks.Infrastructure.Messaging.ServiceBusOptions;
using WorkerServiceBusOptions = RelayWorks.Sync.Worker.ServiceBusOptions;

namespace RelayWorks.ServiceBus.E2E.Tests;

public sealed class ServiceBusWorkerE2ETests
{
    private const string CommandsQueue = "integration-commands";
    private const string EventsTopic = "integration-events";
    private const string ObserverSubscription = "e2e-observer";

    [Fact(Timeout = 120_000)]
    public async Task BrokerRoundTripProjectsResultsAndPreservesTheDurableDeliveryGate()
    {
        var busConnection = RequiredEnvironment("SERVICEBUS_EMULATOR_CONNECTION_STRING");
        var workerSqlConnection = RequiredEnvironment("SERVICEBUS_E2E_SQL_CONNECTION_STRING");
        var controlSqlConnection = RequiredEnvironment("SERVICEBUS_E2E_CONTROL_SQL_CONNECTION_STRING");
        await using var bus = new ServiceBusClient(busConnection);
        await DrainAsync(bus, ObserverSubscription, TestContext.Current.CancellationToken);
        await DrainDeadLettersAsync(bus, TestContext.Current.CancellationToken);

        var destination = new CountingDestination();
        var services = BuildServices(workerSqlConnection, controlSqlConnection, destination);
        await using var provider = services.BuildServiceProvider();
        await ResetDatabasesAsync(provider, TestContext.Current.CancellationToken);

        var workerOptions = Options.Create(new WorkerServiceBusOptions
        {
            ConnectionString = busConnection,
            CommandsQueue = CommandsQueue,
            EventsTopic = EventsTopic
        });
        var controlOptions = Options.Create(new ControlPlaneServiceBusOptions
        {
            ConnectionString = busConnection,
            CommandsQueue = CommandsQueue,
            EventsTopic = EventsTopic,
            EventsSubscription = "control-plane"
        });
        await using var commandWorker = new IntegrationCommandWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), bus, workerOptions,
            TimeProvider.System, NullLogger<IntegrationCommandWorker>.Instance);
        var outboxPublisher = new WorkerOutboxPublisher(
            provider.GetRequiredService<IServiceScopeFactory>(), bus, workerOptions);
        await using var resultConsumer = new IntegrationResultConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(), bus, controlOptions,
            TimeProvider.System, NullLogger<IntegrationResultConsumer>.Instance);

        await commandWorker.StartAsync(TestContext.Current.CancellationToken);
        await outboxPublisher.StartAsync(TestContext.Current.CancellationToken);
        await resultConsumer.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var command = await SeedRunAsync(provider, TestContext.Current.CancellationToken);
            await SendAsync(bus, command, TestContext.Current.CancellationToken);
            var firstEvents = await ReceiveEventsAsync(bus, 2, TestContext.Current.CancellationToken);

            Assert.Contains(nameof(IntegrationRecordResultsReportedV1), firstEvents);
            Assert.Contains(nameof(IntegrationRunCompletedV1), firstEvents);
            await AssertLedgerAsync(provider, expectedInbox: 1, expectedOutbox: 2, expectedWrites: 1,
                destination: destination, cancellationToken: TestContext.Current.CancellationToken);
            await AssertControlPlaneProjectionAsync(provider, command,
                TestContext.Current.CancellationToken);

            // A broker redelivery retains the command MessageId even if the envelope is recreated.
            await SendAsync(bus, command, TestContext.Current.CancellationToken);
            // A new command identity for the same source record crosses the inbox gate and proves
            // the longer-lived record ledger also prevents a second destination write.
            await SendAsync(bus, command with { MessageId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);
            var replayEvents = await ReceiveEventsAsync(bus, 2, TestContext.Current.CancellationToken);

            Assert.Contains(nameof(IntegrationRecordResultsReportedV1), replayEvents);
            Assert.Contains(nameof(IntegrationRunCompletedV1), replayEvents);
            await AssertLedgerAsync(provider, expectedInbox: 2, expectedOutbox: 4, expectedWrites: 1,
                destination: destination, cancellationToken: TestContext.Current.CancellationToken);
            await AssertControlPlaneProjectionAsync(provider, command,
                TestContext.Current.CancellationToken);

            await SendRawAsync(bus, "LegacyIntegrationRunRequested", "{}",
                TestContext.Current.CancellationToken);
            await SendRawAsync(bus, nameof(IntegrationRunRequestedV1), "{not-json",
                TestContext.Current.CancellationToken);
            var deadLetterReasons = await ReceiveDeadLetterReasonsAsync(bus, 2,
                TestContext.Current.CancellationToken);

            Assert.Contains("UnsupportedCommandType", deadLetterReasons);
            Assert.Contains("InvalidCommandPayload", deadLetterReasons);
            await AssertLedgerAsync(provider, expectedInbox: 2, expectedOutbox: 4, expectedWrites: 1,
                destination: destination, cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            await resultConsumer.StopAsync(CancellationToken.None);
            await commandWorker.StopAsync(CancellationToken.None);
            await outboxPublisher.StopAsync(CancellationToken.None);
            outboxPublisher.Dispose();
        }
    }

    private static IServiceCollection BuildServices(string workerSqlConnection,
        string controlSqlConnection, CountingDestination destination)
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
            .AddSingleton(TimeProvider.System)
            .AddDbContext<WorkerLedgerDbContext>(options => options.UseSqlServer(workerSqlConnection))
            .AddDbContext<RelayWorksDbContext>(options => options.UseSqlServer(controlSqlConnection))
            .AddScoped<IIntegrationRunRepository, SqlIntegrationRunRepository>()
            .AddSingleton<ITimeEntrySourceAdapter, SimulatedFieldOperationsAdapter>()
            .AddSingleton<ITimeEntryDestinationAdapter, SimulatedAccountingAdapter>()
            .AddSingleton<ITimeEntryDestinationAdapter, SimulatedPayrollAdapter>()
            .AddSingleton<IAdapterRegistry, AdapterRegistry>()
            .AddScoped<ITimeEntrySourceConnector, RegistryTimeEntrySourceConnector>()
            .AddSingleton<ITimeEntryDestinationConnectorFactory>(new FixedConnectorFactory(destination))
            .AddSingleton<IOptions<ConnectorResilienceOptions>>(resilienceOptions)
            .AddSingleton<IResilienceDelay>(delay)
            .AddSingleton<ConnectionExecutionGate>()
            .AddSingleton<DestinationResilienceExecutor>()
            .AddScoped<TimeEntryProcessor>();
    }

    private static async Task ResetDatabasesAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var workerDb = scope.ServiceProvider.GetRequiredService<WorkerLedgerDbContext>();
        await workerDb.Database.EnsureDeletedAsync(cancellationToken);
        await workerDb.Database.MigrateAsync(cancellationToken);
        var controlDb = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
        await controlDb.Database.EnsureDeletedAsync(cancellationToken);
        await controlDb.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<IntegrationRunRequestedV1> SeedRunAsync(IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var run = IntegrationRun.Create(tenantId, connectionId, IntegrationOperation.TimeEntryExport,
            $"e2e-{Guid.NewGuid():N}", 1, now);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
        db.IntegrationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return new IntegrationRunRequestedV1(
            Guid.NewGuid(), run.Id, tenantId, connectionId, "TimeEntryExport", 1, now,
            new ConnectorExecutionProfileV1("E2E", true, false, 0, "e2e-v1",
                new SecretLocatorV1(new Uri("https://e2e.invalid"), "unused")));
    }

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

    private static async Task SendRawAsync(ServiceBusClient bus, string subject, string payload,
        CancellationToken cancellationToken)
    {
        await using var sender = bus.CreateSender(CommandsQueue);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString(payload))
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = subject,
            ContentType = "application/json"
        }, cancellationToken);
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

    private static async Task DrainDeadLettersAsync(ServiceBusClient bus,
        CancellationToken cancellationToken)
    {
        await using var receiver = bus.CreateReceiver(CommandsQueue,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                SubQueue = SubQueue.DeadLetter
            });
        while (await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(250), cancellationToken) is not null) { }
    }

    private static async Task<HashSet<string>> ReceiveDeadLetterReasonsAsync(ServiceBusClient bus,
        int count, CancellationToken cancellationToken)
    {
        await using var receiver = bus.CreateReceiver(CommandsQueue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var deliveryCounts = new List<int>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (reasons.Count < count && DateTimeOffset.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
            if (message is null) continue;
            if (!string.IsNullOrWhiteSpace(message.DeadLetterReason)) reasons.Add(message.DeadLetterReason);
            deliveryCounts.Add(message.DeliveryCount);
            await receiver.CompleteMessageAsync(message, cancellationToken);
        }
        Assert.Equal(count, reasons.Count);
        Assert.All(deliveryCounts, deliveryCount => Assert.Equal(1, deliveryCount));
        return reasons;
    }

    private static async Task AssertLedgerAsync(IServiceProvider provider, int expectedInbox,
        int expectedOutbox, int expectedWrites, CountingDestination destination,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerLedgerDbContext>();
        Assert.Equal(expectedInbox, await db.InboxMessages.CountAsync(cancellationToken));
        Assert.Equal(expectedOutbox, await db.OutboxMessages.CountAsync(cancellationToken));
        Assert.Single(await db.RecordDeliveries.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Equal(expectedWrites, destination.Writes);
    }

    private static async Task AssertControlPlaneProjectionAsync(IServiceProvider provider,
        IntegrationRunRequestedV1 command, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
            var run = await db.IntegrationRuns.AsNoTracking()
                .SingleAsync(value => value.Id == command.RunId, cancellationToken);
            var records = await db.IntegrationRecordProjections.AsNoTracking()
                .Where(value => value.RunId == command.RunId).ToListAsync(cancellationToken);
            if (run.Status == IntegrationRunStatus.Completed && records.Count == 1)
            {
                Assert.Equal(1, run.AcceptedRecords);
                Assert.Equal(0, run.RejectedRecords);
                Assert.Equal(nameof(RecordDeliveryState.Succeeded), records[0].Status);
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
        throw new TimeoutException("Control Plane did not project the Worker result within 30 seconds.");
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required E2E setting '{name}' was not provided.");

    private sealed class FixedConnectorFactory(ITimeEntryDestinationConnector connector)
        : ITimeEntryDestinationConnectorFactory
    {
        public Task<ITimeEntryDestinationConnector> CreateAsync(ConnectorExecutionProfileV1 profile,
            Guid tenantId, Guid connectionId, CancellationToken cancellationToken) => Task.FromResult(connector);
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
