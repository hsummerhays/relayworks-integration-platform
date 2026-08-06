using System.Text.Json;
using RelayWorks.Application.Abstractions;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Application.IntegrationRuns;

public sealed record SubmitIntegrationRunCommand(
    Guid TenantId,
    Guid ConnectionId,
    IntegrationOperation Operation,
    string IdempotencyKey,
    int TotalRecords);

public sealed record SubmitIntegrationRunResult(IntegrationRunDto Run, bool IsDuplicate);

public sealed class SubmitIntegrationRunHandler(
    IIntegrationRunRepository repository,
    TimeProvider timeProvider)
{
    public async Task<SubmitIntegrationRunResult> HandleAsync(
        SubmitIntegrationRunCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindByIdempotencyKeyAsync(
            command.TenantId,
            command.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return new SubmitIntegrationRunResult(IntegrationRunDto.FromDomain(existing), true);
        }

        var run = IntegrationRun.Create(
            command.TenantId,
            command.ConnectionId,
            command.Operation,
            command.IdempotencyKey,
            command.TotalRecords,
            timeProvider.GetUtcNow());
        var message = new IntegrationRunRequestedV1(
            Guid.NewGuid(),
            run.Id,
            run.TenantId,
            run.ConnectionId,
            run.Operation.ToString(),
            run.TotalRecords,
            timeProvider.GetUtcNow());

        try
        {
            await repository.AddWithOutboxMessageAsync(
                run,
                nameof(IntegrationRunRequestedV1),
                JsonSerializer.Serialize(message),
                cancellationToken);
        }
        catch (DuplicateIntegrationRunException)
        {
            existing = await repository.FindByIdempotencyKeyAsync(
                command.TenantId,
                command.IdempotencyKey,
                cancellationToken);
            if (existing is null) throw;
            return new SubmitIntegrationRunResult(IntegrationRunDto.FromDomain(existing), true);
        }

        return new SubmitIntegrationRunResult(IntegrationRunDto.FromDomain(run), false);
    }
}
