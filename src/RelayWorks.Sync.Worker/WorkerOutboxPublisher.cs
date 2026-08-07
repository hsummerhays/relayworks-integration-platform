using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RelayWorks.Sync.Worker.Persistence;
using RelayWorks.Sync.Worker.Telemetry;
using RelayWorks.Contracts.Telemetry;
using System.Diagnostics;

namespace RelayWorks.Sync.Worker;

public sealed class WorkerOutboxPublisher(IServiceScopeFactory scopeFactory, ServiceBusClient client,
    IOptions<ServiceBusOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sender = client.CreateSender(options.Value.EventsTopic);
        await using (sender)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkerLedgerDbContext>();
                var messages = await db.OutboxMessages.Where(x => x.DispatchedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc).Take(50).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    using var activity = WorkerTelemetry.ActivitySource.StartActivity("servicebus publish event", ActivityKind.Producer);
                    activity?.SetTag("messaging.message.type", message.Type);
                    activity?.SetTag("relayworks.correlation_id", MessageTelemetry.BusinessCorrelationId(message.Payload));
                    var busMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
                    {
                        MessageId = message.Id.ToString(), Subject = message.Type,
                        ContentType = "application/json", CorrelationId = MessageTelemetry.BusinessCorrelationId(message.Payload)
                    };
                    MessageTelemetry.Inject(busMessage.ApplicationProperties);
                    await sender.SendMessageAsync(busMessage, stoppingToken);
                    message.DispatchedAtUtc = DateTimeOffset.UtcNow;
                    WorkerTelemetry.OutboxPublished.Add(1, new("message.type", message.Type));
                }
                if (messages.Count > 0) await db.SaveChangesAsync(stoppingToken);
                else await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
