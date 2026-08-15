using System.Collections.Concurrent;

namespace RelayWorks.Sync.Worker.Adapters;

public interface IAdapterRegistry
{
    ITimeEntrySourceAdapter GetSourceAdapter(string provider);
    ITimeEntryDestinationAdapter GetDestinationAdapter(string provider);
    bool TryGetDestinationAdapter(string provider, out ITimeEntryDestinationAdapter? adapter);
    bool TryGetSourceAdapter(string provider, out ITimeEntrySourceAdapter? adapter);
    IReadOnlyCollection<string> RegisteredSourceProviders { get; }
    IReadOnlyCollection<string> RegisteredDestinationProviders { get; }
}

public sealed class AdapterRegistry : IAdapterRegistry
{
    private readonly ConcurrentDictionary<string, ITimeEntrySourceAdapter> _sourceAdapters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ITimeEntryDestinationAdapter> _destinationAdapters =
        new(StringComparer.OrdinalIgnoreCase);

    public AdapterRegistry(
        IEnumerable<ITimeEntrySourceAdapter> sourceAdapters,
        IEnumerable<ITimeEntryDestinationAdapter> destinationAdapters)
    {
        foreach (var adapter in sourceAdapters)
        {
            if (!_sourceAdapters.TryAdd(adapter.Provider, adapter))
            {
                throw new InvalidOperationException($"Duplicate source adapter registration detected for provider '{adapter.Provider}'.");
            }
        }

        foreach (var adapter in destinationAdapters)
        {
            if (!_destinationAdapters.TryAdd(adapter.Provider, adapter))
            {
                throw new InvalidOperationException($"Duplicate destination adapter registration detected for provider '{adapter.Provider}'.");
            }
        }
    }

    public ITimeEntrySourceAdapter GetSourceAdapter(string provider) =>
        _sourceAdapters.TryGetValue(provider, out var adapter)
            ? adapter
            : throw new NotSupportedException($"Source provider adapter '{provider}' is not registered.");

    public ITimeEntryDestinationAdapter GetDestinationAdapter(string provider) =>
        _destinationAdapters.TryGetValue(provider, out var adapter)
            ? adapter
            : throw new NotSupportedException($"Destination provider adapter '{provider}' is not registered.");

    public bool TryGetSourceAdapter(string provider, out ITimeEntrySourceAdapter? adapter) =>
        _sourceAdapters.TryGetValue(provider, out adapter);

    public bool TryGetDestinationAdapter(string provider, out ITimeEntryDestinationAdapter? adapter) =>
        _destinationAdapters.TryGetValue(provider, out adapter);

    public IReadOnlyCollection<string> RegisteredSourceProviders => _sourceAdapters.Keys.ToArray();
    public IReadOnlyCollection<string> RegisteredDestinationProviders => _destinationAdapters.Keys.ToArray();
}
