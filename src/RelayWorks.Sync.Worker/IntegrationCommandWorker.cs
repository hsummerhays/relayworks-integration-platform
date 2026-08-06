using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker;

public sealed class IntegrationCommandWorker(
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> options,
    TimeEntryProcessor processor,
    TimeProvider timeProvider,
    ILogger<IntegrationCommandWorker> logger) : IHostedService, IAsyncDisposable
{
    private ServiceBusProcessor? _commandProcessor;
    private ServiceBusSender? _eventSender;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _eventSender = serviceBusClient.CreateSender(options.Value.EventsTopic);
        _commandProcessor = serviceBusClient.CreateProcessor(
            options.Value.CommandsQueue,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 8 });
        _commandProcessor.ProcessMessageAsync += ProcessMessageAsync;
        _commandProcessor.ProcessErrorAsync += ProcessErrorAsync;
        await _commandProcessor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_commandProcessor is not null) await _commandProcessor.StopProcessingAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        if (args.Message.Subject != nameof(IntegrationRunRequestedV1))
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "UnsupportedCommandType",
                cancellationToken: args.CancellationToken);
            return;
        }

        var command = args.Message.Body.ToObjectFromJson<IntegrationRunRequestedV1>();
        if (command is null) throw new InvalidOperationException("Integration command payload was empty.");
        if (command.Operation != "TimeEntryExport")
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "UnsupportedOperation",
                command.Operation,
                args.CancellationToken);
            return;
        }

        var result = processor.Process(command, timeProvider.GetUtcNow());
        var resultMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(result))
        {
            MessageId = result.MessageId.ToString(),
            CorrelationId = command.RunId.ToString(),
            Subject = nameof(IntegrationRunCompletedV1),
            ContentType = "application/json"
        };
        await _eventSender!.SendMessageAsync(resultMessage, args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        logger.LogInformation(
            "Processed run {RunId}: {Accepted} accepted, {Rejected} rejected",
            result.RunId,
            result.AcceptedRecords,
            result.RejectedRecords);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus command worker failed at {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_commandProcessor is not null) await _commandProcessor.DisposeAsync();
        if (_eventSender is not null) await _eventSender.DisposeAsync();
    }
}
