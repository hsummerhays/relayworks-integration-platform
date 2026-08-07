using System.Text;
using System.Text.Json;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Application.IntegrationRuns;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor, int PageSize);

public sealed record IntegrationRunQuery(
    Guid TenantId,
    IntegrationRunStatus? Status,
    Guid? ConnectionId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int PageSize,
    DateTimeOffset? CursorTimestamp,
    Guid? CursorId);

public static class PageCursor
{
    public static string Encode(DateTimeOffset timestamp, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CursorValue(timestamp, id))))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? value, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default; id = default;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var cursor = JsonSerializer.Deserialize<CursorValue>(Convert.FromBase64String(base64));
            if (cursor is null || cursor.Id == Guid.Empty) return false;
            timestamp = cursor.Timestamp; id = cursor.Id; return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        { return false; }
    }

    private sealed record CursorValue(DateTimeOffset Timestamp, Guid Id);
}
