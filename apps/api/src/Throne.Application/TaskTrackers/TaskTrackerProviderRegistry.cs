namespace Throne.Application.TaskTrackers;

/// <summary>
/// Default <see cref="ITaskTrackerProviderRegistry"/>. Indexes the registered providers by
/// <see cref="ITaskTrackerProvider.TrackerKey"/> at construction time (ordinal) so lookups are O(1),
/// while <see cref="AllProviders"/> preserves DI registration order for stable catalog output. The
/// empty case is handled so the host boots provider-neutral (no adapter registered yet).
/// </summary>
public sealed class TaskTrackerProviderRegistry : ITaskTrackerProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ITaskTrackerProvider> _byKey;

    public TaskTrackerProviderRegistry(IEnumerable<ITaskTrackerProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var ordered = new List<ITaskTrackerProvider>();
        var map = new Dictionary<string, ITaskTrackerProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (!map.TryAdd(provider.TrackerKey, provider))
            {
                throw new InvalidOperationException(
                    $"Duplicate ITaskTrackerProvider registration for '{provider.TrackerKey}'.");
            }

            ordered.Add(provider);
        }

        _byKey = map;
        AllProviders = ordered;
    }

    public IReadOnlyCollection<ITaskTrackerProvider> AllProviders { get; }

    public ITaskTrackerProvider? GetByName(string trackerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackerKey);
        return _byKey.GetValueOrDefault(trackerKey);
    }
}
