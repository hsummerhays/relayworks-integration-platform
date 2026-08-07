using RelayWorks.Application.Abstractions;
using RelayWorks.Application.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Application.Tests;

public sealed class PagingTests
{
    [Fact]
    public void Cursor_round_trips_timestamp_and_id()
    {
        var timestamp = DateTimeOffset.Parse("2026-08-07T12:34:56.789Z");
        var id = Guid.NewGuid();

        var valid = PageCursor.TryDecode(PageCursor.Encode(timestamp, id), out var decodedTime, out var decodedId);

        Assert.True(valid);
        Assert.Equal(timestamp, decodedTime);
        Assert.Equal(id, decodedId);
    }

    [Fact]
    public async Task Handler_returns_bounded_page_and_cursor_when_an_extra_row_exists()
    {
        var tenant = Guid.NewGuid(); var connection = Guid.NewGuid();
        var rows = Enumerable.Range(0, 3).Select(index => IntegrationRun.Create(tenant, connection,
            IntegrationOperation.TimeEntryExport, $"key-{index}", 1,
            DateTimeOffset.Parse("2026-08-07T12:00:00Z").AddMinutes(-index))).ToList();
        var handler = new ListIntegrationRunsHandler(new FakeRepository(rows));

        var result = await handler.HandleAsync(new IntegrationRunQuery(tenant, null, null, null, null,
            2, null, null), default);

        Assert.Equal(2, result.Items.Count);
        Assert.NotNull(result.NextCursor);
        Assert.Equal(2, result.PageSize);
    }

    private sealed class FakeRepository(IReadOnlyList<IntegrationRun> rows) : IIntegrationRunRepository
    {
        public Task<IReadOnlyList<IntegrationRun>> ListAsync(IntegrationRunQuery query,
            CancellationToken cancellationToken) => Task.FromResult(rows);
        public Task<IntegrationRun?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult<IntegrationRun?>(null);
        public Task<IntegrationRun?> FindByIdAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult<IntegrationRun?>(null);
        public Task AddWithOutboxMessageAsync(IntegrationRun run, string messageType, string messagePayload,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
