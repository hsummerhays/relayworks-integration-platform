using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RelayWorks.Sync.Worker.Persistence;
using RelayWorks.Sync.Worker.Telemetry;

namespace RelayWorks.Sync.Worker.Retention;

public sealed class WorkerRetentionService(IServiceScopeFactory scopeFactory,
    IOptions<WorkerRetentionOptions> options, TimeProvider timeProvider,
    ILogger<WorkerRetentionService> logger) : BackgroundService
{
    private readonly WorkerRetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCycleAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(exception, "Worker retention cycle failed"); WorkerTelemetry.RetentionFailures.Add(1); }
            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
        }
    }

    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerLedgerDbContext>();
        var outboxCutoff = timeProvider.GetUtcNow().AddDays(-_options.DispatchedOutboxDays);
        var inboxCutoff = timeProvider.GetUtcNow().AddDays(-_options.CompletedInboxDays);
        if (_options.DryRun)
        {
            var outbox = await db.OutboxMessages.CountAsync(x => x.DispatchedAtUtc < outboxCutoff, cancellationToken);
            var inbox = await db.InboxMessages.CountAsync(x => x.ProcessedAtUtc < inboxCutoff, cancellationToken);
            logger.LogInformation("Retention dry run found {OutboxRows} outbox and {InboxRows} inbox rows", outbox, inbox);
            return;
        }
        var deletedOutbox = await db.OutboxMessages.Where(x => x.DispatchedAtUtc < outboxCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        var deletedInbox = await db.InboxMessages.Where(x => x.ProcessedAtUtc < inboxCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        WorkerTelemetry.RetentionRowsDeleted.Add(deletedOutbox, new("table", "worker-outbox"));
        WorkerTelemetry.RetentionRowsDeleted.Add(deletedInbox, new("table", "worker-inbox"));
        // RecordDeliveries are intentionally retained as compact, durable idempotency tombstones.
    }
}
