using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker.Persistence;

namespace RelayWorks.Sync.Worker;

public sealed class TimeEntryProcessor(
    ITimeEntrySourceConnector sourceConnector,
    ITimeEntryDestinationConnector destinationConnector,
    WorkerLedgerDbContext dbContext)
{
    public async Task<IntegrationRunCompletedV1> ProcessAsync(
        IntegrationRunRequestedV1 command,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        if (await dbContext.InboxMessages.AnyAsync(x => x.MessageId == command.MessageId, cancellationToken))
            return BuildCompletion(command, await ResultsForRun(command.RunId, cancellationToken), completedAtUtc);

        var reported = new List<IntegrationRecordResultV1>();
        foreach (var entry in sourceConnector.Read(command))
            reported.Add(await ProcessRecord(command, entry, completedAtUtc, cancellationToken));

        dbContext.InboxMessages.Add(new WorkerInboxMessage
        {
            MessageId = command.MessageId,
            ProcessedAtUtc = completedAtUtc
        });

        var recordEvent = new IntegrationRecordResultsReportedV1(
            Guid.NewGuid(), command.RunId, command.TenantId, reported, completedAtUtc);
        var completion = BuildCompletion(command, reported, completedAtUtc);
        AddOutbox(recordEvent.MessageId, nameof(IntegrationRecordResultsReportedV1), recordEvent, completedAtUtc);
        AddOutbox(completion.MessageId, nameof(IntegrationRunCompletedV1), completion, completedAtUtc.AddTicks(1));
        await dbContext.SaveChangesAsync(cancellationToken);
        return completion;
    }

    private async Task<IntegrationRecordResultV1> ProcessRecord(
        IntegrationRunRequestedV1 command,
        CanonicalTimeEntryV1 entry,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fingerprint = Fingerprint(entry);
        var existing = await dbContext.RecordDeliveries.SingleOrDefaultAsync(x =>
            x.TenantId == command.TenantId && x.ConnectionId == command.ConnectionId &&
            x.Operation == command.Operation && x.SourceRecordId == entry.SourceRecordId &&
            x.SourceVersion == entry.SourceVersion, cancellationToken);

        if (existing is not null)
        {
            if (existing.CanonicalFingerprint != fingerprint)
            {
                existing.Finish(RecordDeliveryState.UnknownOutcome, existing.DestinationReference,
                    "SOURCE_VERSION_CONFLICT",
                    "The same source version produced different canonical data; operator review is required.", now);
                return Result(entry, RecordDeliveryState.UnknownOutcome, null, "SOURCE_VERSION_CONFLICT",
                    "The same source version produced different canonical data; operator review is required.", now);
            }

            if (existing.State == RecordDeliveryState.Processing)
                existing.Finish(RecordDeliveryState.UnknownOutcome, null, "INTERRUPTED_WRITE",
                    "Processing was interrupted after the delivery gate was recorded; verify the destination before resolving.", now);

            // Terminal records are the durable idempotency gate: no second destination write.
            return Result(entry, existing.State, existing.DestinationReference,
                existing.ErrorCode, existing.ErrorMessage, now);
        }

        var delivery = RecordDelivery.Start(command.TenantId, command.ConnectionId, command.RunId,
            command.Operation, entry.SourceRecordId, entry.SourceVersion, fingerprint,
            entry.EmployeeReference, entry.ProjectReference, now);
        dbContext.RecordDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);

        var write = destinationConnector.Write(entry);
        var state = write.Status switch
        {
            DestinationWriteStatus.Succeeded => RecordDeliveryState.Succeeded,
            DestinationWriteStatus.Rejected => RecordDeliveryState.Rejected,
            DestinationWriteStatus.UnknownOutcome => RecordDeliveryState.UnknownOutcome,
            _ => throw new ArgumentOutOfRangeException()
        };
        delivery.Finish(state, write.DestinationReference, write.ErrorCode, write.ErrorMessage, now);
        return Result(entry, state, write.DestinationReference, write.ErrorCode, write.ErrorMessage, now);
    }

    private async Task<List<IntegrationRecordResultV1>> ResultsForRun(Guid runId, CancellationToken cancellationToken) =>
        await dbContext.RecordDeliveries.Where(x => x.RunId == runId)
            .Select(x => new IntegrationRecordResultV1(x.SourceRecordId, x.SourceVersion,
                x.EmployeeReference, x.ProjectReference, x.State.ToString(), x.ErrorCode,
                x.ErrorMessage, x.DestinationReference, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    private static IntegrationRunCompletedV1 BuildCompletion(IntegrationRunRequestedV1 command,
        IReadOnlyCollection<IntegrationRecordResultV1> results, DateTimeOffset now) =>
        new(Guid.NewGuid(), command.RunId, command.TenantId,
            results.Count(x => x.Status == nameof(RecordDeliveryState.Succeeded)),
            results.Count(x => x.Status != nameof(RecordDeliveryState.Succeeded)), now);

    private void AddOutbox<T>(Guid id, string type, T payload, DateTimeOffset now) =>
        dbContext.OutboxMessages.Add(new WorkerOutboxMessage
        {
            Id = id, Type = type, Payload = JsonSerializer.Serialize(payload), OccurredAtUtc = now
        });

    private static IntegrationRecordResultV1 Result(CanonicalTimeEntryV1 entry, RecordDeliveryState state,
        string? reference, string? code, string? message, DateTimeOffset now) =>
        new(entry.SourceRecordId, entry.SourceVersion, entry.EmployeeReference, entry.ProjectReference,
            state.ToString(), code, message, reference, now);

    private static string Fingerprint(CanonicalTimeEntryV1 entry)
    {
        var canonical = string.Join('|', entry.TenantId, entry.SourceRecordId, entry.SourceVersion,
            entry.EmployeeReference, entry.ProjectReference, entry.WorkDate.ToString("O"),
            entry.RegularHours, entry.OvertimeHours, entry.LaborCode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
