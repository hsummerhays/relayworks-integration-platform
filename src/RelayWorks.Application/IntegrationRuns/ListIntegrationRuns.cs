using RelayWorks.Application.Abstractions;

namespace RelayWorks.Application.IntegrationRuns;

public sealed class ListIntegrationRunsHandler(IIntegrationRunRepository repository)
{
    public async Task<PagedResult<IntegrationRunDto>> HandleAsync(
        IntegrationRunQuery query,
        CancellationToken cancellationToken)
    {
        var runs = await repository.ListAsync(query with { PageSize = query.PageSize + 1 }, cancellationToken);
        var hasMore = runs.Count > query.PageSize;
        var page = runs.Take(query.PageSize).ToList();
        var next = hasMore && page.Count > 0
            ? PageCursor.Encode(page[^1].CreatedAtUtc, page[^1].Id)
            : null;
        return new(page.Select(IntegrationRunDto.FromDomain).ToList(), next, query.PageSize);
    }
}
