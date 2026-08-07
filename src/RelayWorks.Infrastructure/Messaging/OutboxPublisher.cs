using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.Infrastructure.Telemetry;
using RelayWorks.Contracts.Telemetry;
using System.Diagnostics;

namespace RelayWorks.Infrastructure.Messaging;

public sealed partial class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sender = serviceBusClient.CreateSender(options.Value.CommandsQueue);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PublishBatchAsync(sender, stoppingToken);
        }
    }

    private async Task PublishBatchAsync(ServiceBusSender sender, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
        var messages = await dbContext.OutboxMessages
            .Where(message => message.DispatchedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            using var activity = ControlPlaneTelemetry.ActivitySource.StartActivity(
                "servicebus publish command", ActivityKind.Producer);
            activity?.SetTag("messaging.destination.name", options.Value.CommandsQueue);
            activity?.SetTag("messaging.message.type", message.Type);
            activity?.SetTag("relayworks.correlation_id", MessageTelemetry.BusinessCorrelationId(message.Payload));
            try
            {
                message.RecordAttempt();
                var busMessage = new ServiceBusMessage(message.Payload)
                {
                    MessageId = message.Id.ToString(),
                    Subject = message.Type,
                    ContentType = "application/json"
                };
                busMessage.CorrelationId = MessageTelemetry.BusinessCorrelationId(message.Payload);
                MessageTelemetry.Inject(busMessage.ApplicationProperties);
                await sender.SendMessageAsync(busMessage, cancellationToken);
                message.MarkDispatched(timeProvider.GetUtcNow());
                ControlPlaneTelemetry.OutboxPublished.Add(1, new KeyValuePair<string, object?>("message.type", message.Type));
                ControlPlaneTelemetry.OutboxLag.Record((timeProvider.GetUtcNow() - message.OccurredAtUtc).TotalSeconds,
                    new KeyValuePair<string, object?>("message.type", message.Type));
            }
            catch (Exception exception)
            {
                LogDispatchFailed(logger, exception, message.Id);
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                ControlPlaneTelemetry.OutboxFailures.Add(1, new KeyValuePair<string, object?>("message.type", message.Type));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch outbox message {MessageId}")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception, Guid messageId);
}
