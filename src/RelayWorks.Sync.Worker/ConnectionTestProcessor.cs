using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Contracts.Connections;
using RelayWorks.Sync.Worker.Persistence;
using RelayWorks.Sync.Worker.Telemetry;

namespace RelayWorks.Sync.Worker;

public sealed partial class ConnectionTestProcessor(ITimeEntryDestinationConnectorFactory factory,
    WorkerLedgerDbContext db, TimeProvider timeProvider, ILogger<ConnectionTestProcessor> logger)
{
    public async Task ProcessAsync(ConnectionTestRequestedV1 command, CancellationToken cancellationToken)
    {
        if (await db.InboxMessages.AnyAsync(x => x.MessageId == command.MessageId, cancellationToken)) return;
        var stopwatch = Stopwatch.StartNew();
        using var activity = WorkerTelemetry.ActivitySource.StartActivity("connector test", ActivityKind.Client);
        activity?.SetTag("relayworks.test_id", command.TestId);
        activity?.SetTag("connector.provider", command.ConnectorProfile.Provider);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        string status; string? category = null; string? safeMessage;
        try
        {
            var connector = await factory.CreateAsync(command.ConnectorProfile, command.TenantId, command.ConnectionId, timeout.Token);
            var health = await connector.TestConnectionAsync(timeout.Token);
            status = health.Succeeded ? "Succeeded" : "Failed";
            category = health.FailureCategory; safeMessage = health.SafeMessage;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { status = "TimedOut"; category = "Timeout"; safeMessage = "The provider did not respond within 30 seconds."; }
        catch (OperationCanceledException) { throw; }
        catch (Azure.RequestFailedException exception)
        {
            status = "Failed"; category = exception.Status is 401 or 403 ? "CredentialOrPermission" : "ProviderService";
            safeMessage = "Credential resolution or provider access failed. Review the connection and Worker diagnostics.";
            LogConnectionTestFailed(logger, exception, command.TestId);
        }
        catch (Exception exception)
        {
            status = "Failed"; category = "ConfigurationOrNetwork";
            safeMessage = "The connection test failed. Review configuration and Worker diagnostics.";
            LogConnectionTestFailed(logger, exception, command.TestId);
        }
        stopwatch.Stop(); var now = timeProvider.GetUtcNow(); var eventId = Guid.NewGuid();
        activity?.SetTag("connector.test.status", status);
        if (status != "Succeeded") activity?.SetStatus(ActivityStatusCode.Error, category);
        WorkerTelemetry.ConnectorDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
            new("provider", command.ConnectorProfile.Provider), new("outcome", status));
        var result = new ConnectionTestCompletedV1(eventId, command.TestId, command.TenantId,
            command.ConnectionId, status, category, safeMessage, stopwatch.Elapsed, now);
        db.InboxMessages.Add(new WorkerInboxMessage { MessageId = command.MessageId, ProcessedAtUtc = now });
        db.OutboxMessages.Add(new WorkerOutboxMessage { Id = eventId, Type = nameof(ConnectionTestCompletedV1),
            Payload = JsonSerializer.Serialize(result), OccurredAtUtc = now });
        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection test {TestId} failed")]
    private static partial void LogConnectionTestFailed(ILogger logger, Exception exception, Guid testId);
}
