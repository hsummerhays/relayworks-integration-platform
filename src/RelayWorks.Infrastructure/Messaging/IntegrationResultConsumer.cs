using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RelayWorks.Application.Abstractions;
using RelayWorks.Contracts.IntegrationRuns;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.Contracts.Connections;
using RelayWorks.Contracts.Telemetry;
using RelayWorks.Infrastructure.Telemetry;
using System.Diagnostics;

namespace RelayWorks.Infrastructure.Messaging;

public sealed partial class IntegrationResultConsumer(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> options,
    TimeProvider timeProvider,
    ILogger<IntegrationResultConsumer> logger) : IHostedService, IAsyncDisposable
{
    private ServiceBusProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = serviceBusClient.CreateProcessor(
            options.Value.EventsTopic,
            options.Value.EventsSubscription,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 4 });
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;
        await _processor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null) await _processor.StopProcessingAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var parent = MessageTelemetry.Extract(args.Message.ApplicationProperties);
        using var activity = ControlPlaneTelemetry.ActivitySource.StartActivity("servicebus project event",
            ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.message.type", args.Message.Subject);
        activity?.SetTag("relayworks.correlation_id", args.Message.CorrelationId);
        activity?.SetTag("messaging.delivery_count", args.Message.DeliveryCount);
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationRunRepository>();

        if (args.Message.Subject == nameof(ConnectionTestCompletedV1))
        {
            var result = args.Message.Body.ToObjectFromJson<ConnectionTestCompletedV1>()
                ?? throw new InvalidOperationException("Connection test result payload was empty.");
            var db = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
            var test = await db.ConnectionTests.SingleOrDefaultAsync(x => x.Id == result.TestId &&
                x.TenantId == result.TenantId, args.CancellationToken);
            if (test is null)
            {
                await args.DeadLetterMessageAsync(args.Message, "UnknownConnectionTest", cancellationToken: args.CancellationToken);
                return;
            }
            test.Complete(result.Status, result.FailureCategory, result.SafeMessage, result.Duration, result.OccurredAtUtc);
            db.OperatorAuditRecords.Add(new OperatorAuditRecord { TenantId = result.TenantId,
                ActorId = "sync-worker", Action = "ConnectionTestCompleted", ResourceType = "ConnectionTest",
                ResourceId = result.TestId.ToString(), Detail = result.Status, OccurredAtUtc = result.OccurredAtUtc });
            await db.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            ControlPlaneTelemetry.EventsProjected.Add(1, new KeyValuePair<string, object?>("event.type", nameof(ConnectionTestCompletedV1)));
            return;
        }

        if (args.Message.Subject == nameof(IntegrationRecordResultsReportedV1))
        {
            var report = args.Message.Body.ToObjectFromJson<IntegrationRecordResultsReportedV1>()
                ?? throw new InvalidOperationException("Record result event payload was empty.");
            var db = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
            foreach (var record in report.Records)
            {
                var projection = await db.IntegrationRecordProjections.SingleOrDefaultAsync(x =>
                    x.RunId == report.RunId && x.SourceRecordId == record.SourceRecordId &&
                    x.SourceVersion == record.SourceVersion, args.CancellationToken);
                if (projection is null)
                    db.IntegrationRecordProjections.Add(IntegrationRecordProjection.Create(report.RunId,
                        report.TenantId, record.SourceRecordId, record.SourceVersion,
                        record.EmployeeReference, record.ProjectReference, record.Status,
                        record.ErrorCode, record.ErrorMessage, record.DestinationReference, record.OccurredAtUtc));
                else
                    projection.Update(record.Status, record.ErrorCode, record.ErrorMessage,
                        record.DestinationReference, record.OccurredAtUtc);
            }
            await db.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            ControlPlaneTelemetry.EventsProjected.Add(1, new KeyValuePair<string, object?>("event.type", nameof(IntegrationRecordResultsReportedV1)));
            return;
        }

        if (args.Message.Subject == nameof(IntegrationRunCompletedV1))
        {
            var result = args.Message.Body.ToObjectFromJson<IntegrationRunCompletedV1>();
            if (result is null) throw new InvalidOperationException("Completion event payload was empty.");
            var run = await repository.FindByIdAsync(result.RunId, args.CancellationToken);
            if (run is null)
            {
                LogUnknownRunCompletion(logger, result.RunId);
                await args.DeadLetterMessageAsync(args.Message, "UnknownRun", cancellationToken: args.CancellationToken);
                return;
            }

            if (run.Status is RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.Completed
                or RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.CompletedWithErrors)
            {
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            if (run.Status == RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.Pending) run.Start();
            run.Complete(result.AcceptedRecords, result.RejectedRecords, result.OccurredAtUtc);
            await repository.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            ControlPlaneTelemetry.EventsProjected.Add(1, new KeyValuePair<string, object?>("event.type", nameof(IntegrationRunCompletedV1)));
            return;
        }

        if (args.Message.Subject == nameof(IntegrationRunFailedV1))
        {
            var result = args.Message.Body.ToObjectFromJson<IntegrationRunFailedV1>();
            if (result is null) throw new InvalidOperationException("Failure event payload was empty.");
            var run = await repository.FindByIdAsync(result.RunId, args.CancellationToken);
            if (run is null)
            {
                await args.DeadLetterMessageAsync(args.Message, "UnknownRun", cancellationToken: args.CancellationToken);
                return;
            }

            if (run.Status == RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.Failed)
            {
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            run.Fail(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            ControlPlaneTelemetry.EventsProjected.Add(1, new KeyValuePair<string, object?>("event.type", nameof(IntegrationRunFailedV1)));
            return;
        }

        await args.DeadLetterMessageAsync(args.Message, "UnsupportedEventType", cancellationToken: args.CancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        LogConsumerError(logger, args.Exception, args.ErrorSource);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Completion received for unknown run {RunId}")]
    private static partial void LogUnknownRunCompletion(ILogger logger, Guid runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus result consumer failed at {ErrorSource}")]
    private static partial void LogConsumerError(ILogger logger, Exception exception, ServiceBusErrorSource errorSource);
}
