namespace Throne.Domain.Capabilities;

/// <summary>
/// Singleton aggregate that persists per-capability <c>enabled</c> toggles. Detection
/// state (whether the prerequisite tool was found at runtime) lives in an in-memory
/// TTL cache in Application — only the operator-controlled toggle ends up in Mongo.
///
/// Default for any capability that has never been touched is <c>false</c>; absence
/// from <see cref="Toggles"/> means «not yet persisted, treat as off». The aggregate
/// itself does not enumerate the set of known names — Application materialises
/// missing rows on read.
/// </summary>
public sealed class Capabilities
{
    /// <summary>
    /// There is exactly one row in the <c>capabilities</c> Mongo singleton. The id is
    /// a fixed sentinel so that <c>findOne({_id: "singleton"})</c> resolves to it.
    /// </summary>
    public const string SingletonId = "singleton";

    private readonly Dictionary<string, bool> _toggles;

    private Capabilities(int currentVersion, DateTimeOffset updatedAt, Dictionary<string, bool> toggles)
    {
        CurrentVersion = currentVersion;
        UpdatedAt = updatedAt;
        _toggles = toggles;
    }

    public int CurrentVersion { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Snapshot of the persisted toggle map. Names always pass <see cref="CapabilityNames.IsKnown"/>.</summary>
    public IReadOnlyDictionary<string, bool> Toggles => _toggles;

    public static Capabilities CreateEmpty(DateTimeOffset now) =>
        new(currentVersion: 1, now, []);

    public static Capabilities Restore(
        int currentVersion,
        DateTimeOffset updatedAt,
        IReadOnlyDictionary<string, bool> toggles)
    {
        ArgumentNullException.ThrowIfNull(toggles);
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        var sanitized = new Dictionary<string, bool>(toggles.Count, StringComparer.Ordinal);
        foreach (var (name, enabled) in toggles)
        {
            if (!CapabilityNames.IsKnown(name))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(toggles),
                    $"Unknown capability name in storage: '{name}'.");
            }

            sanitized[name] = enabled;
        }

        return new Capabilities(currentVersion, updatedAt, sanitized);
    }

    public bool IsEnabled(string name)
    {
        EnsureKnown(name);
        return _toggles.TryGetValue(name, out var enabled) && enabled;
    }

    /// <summary>
    /// Persist a new value for <paramref name="name"/>. Returns <c>true</c> if the
    /// stored value changed (caller persists + bumps the document); a no-op call
    /// returns <c>false</c> so the HTTP layer can skip the write.
    /// </summary>
    public bool SetEnabled(string name, bool enabled, DateTimeOffset now)
    {
        EnsureKnown(name);
        if (_toggles.TryGetValue(name, out var current) && current == enabled)
        {
            return false;
        }

        _toggles[name] = enabled;
        CurrentVersion += 1;
        UpdatedAt = now;
        return true;
    }

    private static void EnsureKnown(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!CapabilityNames.IsKnown(name))
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                $"Unknown capability name: '{name}'.");
        }
    }
}
