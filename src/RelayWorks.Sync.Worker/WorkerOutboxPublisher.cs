using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RelayWorks.Sync.Worker.Persistence;

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
                    await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString(message.Payload))
                    {
                        MessageId = message.Id.ToString(), Subject = message.Type,
                        ContentType = "application/json"
                    }, stoppingToken);
                    message.DispatchedAtUtc = DateTimeOffset.UtcNow;
                }
                if (messages.Count > 0) await db.SaveChangesAsync(stoppingToken);
                else await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
