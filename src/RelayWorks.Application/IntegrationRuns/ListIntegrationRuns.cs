using RelayWorks.Application.Abstractions;

namespace RelayWorks.Application.IntegrationRuns;

public sealed class ListIntegrationRunsHandler(IIntegrationRunRepository repository)
{
    public async Task<IReadOnlyList<IntegrationRunDto>> HandleAsync(
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var runs = await repository.ListAsync(tenantId, cancellationToken);
        return runs.Select(IntegrationRunDto.FromDomain).ToList();
    }
}
