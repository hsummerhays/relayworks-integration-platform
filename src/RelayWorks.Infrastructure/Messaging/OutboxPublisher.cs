using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RelayWorks.Infrastructure.Persistence;

namespace RelayWorks.Infrastructure.Messaging;

public sealed class OutboxPublisher(
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
            try
            {
                message.RecordAttempt();
                var busMessage = new ServiceBusMessage(message.Payload)
                {
                    MessageId = message.Id.ToString(),
                    Subject = message.Type,
                    ContentType = "application/json"
                };
                await sender.SendMessageAsync(busMessage, cancellationToken);
                message.MarkDispatched(timeProvider.GetUtcNow());
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to dispatch outbox message {MessageId}", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
