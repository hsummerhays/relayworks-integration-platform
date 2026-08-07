using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.Infrastructure.Telemetry;

namespace RelayWorks.Infrastructure.Archival;

public sealed class ControlPlaneArchiveWorker(IServiceScopeFactory scopeFactory,
    BlobContainerClient container, IOptions<ArchiveOptions> options, TimeProvider timeProvider,
    ILogger<ControlPlaneArchiveWorker> logger) : BackgroundService
{
    private readonly ArchiveOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ArchiveBatchAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(exception, "Control Plane archive cycle failed"); ControlPlaneTelemetry.ArchiveFailures.Add(1); }
            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
        }
    }

    internal async Task ArchiveBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
        var cutoff = timeProvider.GetUtcNow().AddDays(-_options.SuccessfulRunRetentionDays);
        var candidates = await db.IntegrationRuns.AsNoTracking()
            .Where(run => run.CompletedAtUtc < cutoff &&
                (run.Status == IntegrationRunStatus.Completed || run.Status == IntegrationRunStatus.CompletedWithErrors ||
                 run.Status == IntegrationRunStatus.Failed) &&
                !db.IntegrationRecordProjections.Any(record => record.RunId == run.Id &&
                    (record.Status == "UnknownOutcome" || record.Status == "Rejected")))
            .OrderBy(run => run.CompletedAtUtc).Take(_options.BatchSize).ToListAsync(cancellationToken);

        if (_options.DryRun)
        {
            logger.LogInformation("Archive dry run found {CandidateCount} eligible terminal runs before {Cutoff}", candidates.Count, cutoff);
            ControlPlaneTelemetry.ArchiveCandidates.Add(candidates.Count); return;
        }

        foreach (var run in candidates)
        {
            var records = await db.IntegrationRecordProjections.AsNoTracking()
                .Where(record => record.RunId == run.Id && record.TenantId == run.TenantId)
                .OrderBy(record => record.Id).ToListAsync(cancellationToken);
            var payload = new ArchivePayload(1, run, records, timeProvider.GetUtcNow());
            await using var compressed = new MemoryStream();
            await using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
                await JsonSerializer.SerializeAsync(gzip, payload, cancellationToken: cancellationToken);
            var bytes = compressed.ToArray(); var hash = SHA256.HashData(bytes);
            var completed = run.CompletedAtUtc!.Value;
            var prefix = $"tenant={run.TenantId:N}/year={completed:yyyy}/month={completed:MM}/run={run.Id:N}";
            var dataBlob = container.GetBlobClient($"{prefix}.json.gz");
            await dataBlob.UploadAsync(BinaryData.FromBytes(bytes), overwrite: true,
                cancellationToken: cancellationToken);
            await dataBlob.SetHttpHeadersAsync(new BlobHttpHeaders
            { ContentType = "application/json", ContentEncoding = "gzip", ContentHash = hash }, cancellationToken: cancellationToken);
            await dataBlob.SetMetadataAsync(new Dictionary<string, string>
            { ["schemaVersion"] = "1", ["runId"] = run.Id.ToString("N") }, cancellationToken: cancellationToken);
            var properties = await dataBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!properties.Value.ContentHash.SequenceEqual(hash) || properties.Value.ContentLength != bytes.Length)
                throw new InvalidOperationException($"Archive verification failed for run {run.Id}.");

            var manifest = new ArchiveManifest(1, run.Id, run.TenantId, records.Count, bytes.Length,
                Convert.ToHexString(hash), timeProvider.GetUtcNow(), $"{prefix}.json.gz");
            await container.GetBlobClient($"{prefix}.manifest.json")
                .UploadAsync(BinaryData.FromObjectAsJson(manifest), overwrite: true,
                    cancellationToken: cancellationToken);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.IntegrationRecordProjections.Where(record => record.RunId == run.Id).ExecuteDeleteAsync(cancellationToken);
            await db.IntegrationRuns.Where(candidate => candidate.Id == run.Id && candidate.TenantId == run.TenantId)
                .ExecuteDeleteAsync(cancellationToken);
            db.OperatorAuditRecords.Add(new OperatorAuditRecord
            {
                TenantId = run.TenantId, ActorId = "relayworks-archive", Action = "IntegrationRunArchived",
                ResourceType = "IntegrationRun", ResourceId = run.Id.ToString(), Detail = manifest.BlobName,
                OccurredAtUtc = timeProvider.GetUtcNow()
            });
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            ControlPlaneTelemetry.RunsArchived.Add(1); ControlPlaneTelemetry.RecordsArchived.Add(records.Count);
        }

        var outboxCutoff = timeProvider.GetUtcNow().AddDays(-_options.DispatchedOutboxRetentionDays);
        var deleted = await db.OutboxMessages.Where(message => message.DispatchedAtUtc < outboxCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        ControlPlaneTelemetry.RetentionRowsDeleted.Add(deleted, new("table", "control-outbox"));
    }

    private sealed record ArchivePayload(int SchemaVersion, IntegrationRun Run,
        IReadOnlyList<IntegrationRecordProjection> Records, DateTimeOffset ArchivedAtUtc);
    private sealed record ArchiveManifest(int SchemaVersion, Guid RunId, Guid TenantId, int RecordCount,
        long CompressedBytes, string Sha256, DateTimeOffset ArchivedAtUtc, string BlobName);
}
