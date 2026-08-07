using System.Diagnostics;
using System.Text.Json;

namespace RelayWorks.Contracts.Telemetry;

public static class MessageTelemetry
{
    public static void Inject(IDictionary<string, object> properties)
    {
        var activity = Activity.Current;
        if (activity?.Id is not null) properties[TelemetryNames.TraceParentProperty] = activity.Id;
        if (!string.IsNullOrWhiteSpace(activity?.TraceStateString))
            properties[TelemetryNames.TraceStateProperty] = activity.TraceStateString;
    }

    public static ActivityContext Extract(IReadOnlyDictionary<string, object> properties)
    {
        var parent = properties.TryGetValue(TelemetryNames.TraceParentProperty, out var value) ? value?.ToString() : null;
        var state = properties.TryGetValue(TelemetryNames.TraceStateProperty, out var stateValue) ? stateValue?.ToString() : null;
        return ActivityContext.TryParse(parent, state, out var context) ? context : default;
    }

    public static string? BusinessCorrelationId(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            foreach (var name in new[] { "RunId", "runId", "TestId", "testId" })
                if (document.RootElement.TryGetProperty(name, out var value)) return value.ToString();
        }
        catch (JsonException) { }
        return null;
    }
}
