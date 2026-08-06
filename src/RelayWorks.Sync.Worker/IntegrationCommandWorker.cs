using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker;

public sealed class IntegrationCommandWorker(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> options,
    TimeProvider timeProvider,
    ILogger<IntegrationCommandWorker> logger) : IHostedService, IAsyncDisposable
{
    private ServiceBusProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = serviceBusClient.CreateProcessor(options.Value.CommandsQueue,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 8 });
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus command worker failed at {ErrorSource}", args.ErrorSource);
            return Task.CompletedTask;
        };
        await _processor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null) await _processor.StopProcessingAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        if (args.Message.Subject != nameof(IntegrationRunRequestedV1))
        {
            await args.DeadLetterMessageAsync(args.Message, "UnsupportedCommandType", cancellationToken: args.CancellationToken);
            return;
        }

        var command = args.Message.Body.ToObjectFromJson<IntegrationRunRequestedV1>()
            ?? throw new InvalidOperationException("Integration command payload was empty.");
        if (command.Operation != "TimeEntryExport")
        {
            await args.DeadLetterMessageAsync(args.Message, "UnsupportedOperation", command.Operation, args.CancellationToken);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<TimeEntryProcessor>();
        var result = await processor.ProcessAsync(command, timeProvider.GetUtcNow(), args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        logger.LogInformation("Persisted run {RunId}: {Accepted} accepted, {Rejected} requiring attention",
            result.RunId, result.AcceptedRecords, result.RejectedRecords);
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
    }
}
