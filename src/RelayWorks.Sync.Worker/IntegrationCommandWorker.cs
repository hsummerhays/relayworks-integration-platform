using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.Connections;
using RelayWorks.Contracts.Telemetry;
using RelayWorks.Sync.Worker.Telemetry;
using System.Diagnostics;
using System.Text.Json;

namespace RelayWorks.Sync.Worker;

public sealed partial class IntegrationCommandWorker(
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
            LogProcessorError(logger, args.Exception, args.ErrorSource);
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
        var parent = MessageTelemetry.Extract(args.Message.ApplicationProperties);
        using var activity = WorkerTelemetry.ActivitySource.StartActivity("servicebus process command",
            ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.message.type", args.Message.Subject);
        activity?.SetTag("relayworks.correlation_id", args.Message.CorrelationId);
        activity?.SetTag("messaging.delivery_count", args.Message.DeliveryCount);
        if (args.Message.Subject == nameof(ConnectionTestRequestedV1))
        {
            ConnectionTestRequestedV1? testCommand;
            try
            {
                testCommand = args.Message.Body.ToObjectFromJson<ConnectionTestRequestedV1>();
            }
            catch (JsonException exception)
            {
                await DeadLetterInvalidPayloadAsync(args, exception);
                return;
            }
            if (testCommand is null || testCommand.MessageId == Guid.Empty ||
                testCommand.TestId == Guid.Empty || testCommand.TenantId == Guid.Empty ||
                testCommand.ConnectionId == Guid.Empty || !IsValidProfile(testCommand.ConnectorProfile))
            {
                await DeadLetterInvalidPayloadAsync(args, null);
                return;
            }
            await using var testScope = scopeFactory.CreateAsyncScope();
            await testScope.ServiceProvider.GetRequiredService<ConnectionTestProcessor>()
                .ProcessAsync(testCommand, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            WorkerTelemetry.CommandsProcessed.Add(1, new KeyValuePair<string, object?>("command.type", nameof(ConnectionTestRequestedV1)));
            LogConnectionTestProcessed(logger, testCommand.TestId);
            return;
        }

        if (args.Message.Subject != nameof(IntegrationRunRequestedV1))
        {
            await args.DeadLetterMessageAsync(args.Message, "UnsupportedCommandType", cancellationToken: args.CancellationToken);
            return;
        }

        IntegrationRunRequestedV1? command;
        try
        {
            command = args.Message.Body.ToObjectFromJson<IntegrationRunRequestedV1>();
        }
        catch (JsonException exception)
        {
            await DeadLetterInvalidPayloadAsync(args, exception);
            return;
        }
        if (command is null || command.MessageId == Guid.Empty || command.RunId == Guid.Empty ||
            command.TenantId == Guid.Empty || command.ConnectionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.Operation) || command.TotalRecords is <= 0 or > 10_000 ||
            !IsValidProfile(command.ConnectorProfile))
        {
            await DeadLetterInvalidPayloadAsync(args, null);
            return;
        }
        if (command.Operation != "TimeEntryExport")
        {
            await args.DeadLetterMessageAsync(args.Message, "UnsupportedOperation", command.Operation, args.CancellationToken);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<TimeEntryProcessor>();
        var result = await processor.ProcessAsync(command, timeProvider.GetUtcNow(), args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        WorkerTelemetry.CommandsProcessed.Add(1, new KeyValuePair<string, object?>("command.type", nameof(IntegrationRunRequestedV1)));
        LogRunPersisted(logger, result.RunId, result.AcceptedRecords, result.RejectedRecords);
    }

    private async Task DeadLetterInvalidPayloadAsync(ProcessMessageEventArgs args, Exception? exception)
    {
        LogInvalidCommandPayload(logger, exception, args.Message.Subject);
        await args.DeadLetterMessageAsync(args.Message, "InvalidCommandPayload",
            "The command payload is malformed or missing required fields.", args.CancellationToken);
    }

    private static bool IsValidProfile(ConnectorExecutionProfileV1? profile) =>
        profile is not null &&
        !string.IsNullOrWhiteSpace(profile.Provider) &&
        !string.IsNullOrWhiteSpace(profile.ConfigurationVersion) &&
        profile.MaxConfirmedNoCommitRetries >= 0 &&
        profile.Secret is not null &&
        profile.Secret.VaultUri is not null &&
        profile.Secret.VaultUri.IsAbsoluteUri &&
        string.Equals(profile.Secret.VaultUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(profile.Secret.SecretName);

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus command worker failed at {ErrorSource}")]
    private static partial void LogProcessorError(ILogger logger, Exception exception, ServiceBusErrorSource errorSource);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed connection test {TestId}")]
    private static partial void LogConnectionTestProcessed(ILogger logger, Guid testId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Persisted run {RunId}: {Accepted} accepted, {Rejected} requiring attention")]
    private static partial void LogRunPersisted(ILogger logger, Guid runId, int accepted, int rejected);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead-lettering invalid command payload for {CommandType}")]
    private static partial void LogInvalidCommandPayload(ILogger logger, Exception? exception, string commandType);
}
