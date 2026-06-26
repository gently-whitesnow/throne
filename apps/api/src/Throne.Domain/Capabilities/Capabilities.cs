namespace Throne.Domain.Capabilities;

/// <summary>
/// Singleton aggregate that persists, for each carrier capability, the operator-selected
/// provider (e.g. <c>open_in_ide → vscode</c>). Detection of provider prerequisites lives
/// in an in-memory TTL cache in Application — only the chosen-provider mapping ends up in persistence.
///
/// Absence from <see cref="Selections"/> means «no explicit selection»; consumers fall back
/// to «whichever provider is detected» when only one candidate exists.
/// </summary>
public sealed class Capabilities
{
    public const string SingletonId = "singleton";

    private readonly Dictionary<string, string> _selections;

    private Capabilities(int currentVersion, DateTimeOffset updatedAt, Dictionary<string, string> selections)
    {
        CurrentVersion = currentVersion;
        UpdatedAt = updatedAt;
        _selections = selections;
    }

    public int CurrentVersion { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Snapshot of the persisted selection map. Names always pass <see cref="CapabilityNames.IsKnown"/>.</summary>
    public IReadOnlyDictionary<string, string> Selections => _selections;

    public static Capabilities CreateEmpty(DateTimeOffset now) =>
        new(currentVersion: 1, now, []);

    public static Capabilities Restore(
        int currentVersion,
        DateTimeOffset updatedAt,
        IReadOnlyDictionary<string, string?> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        // Stale rows from previous capability shapes (`vscode`, `gitlab`, …) are best-effort
        // dropped on read so a no-op upgrade does not require a migration. Empty / null
        // provider strings collapse to «no selection».
        var sanitized = new Dictionary<string, string>(selections.Count, StringComparer.Ordinal);
        foreach (var (name, provider) in selections)
        {
            if (!CapabilityNames.IsKnown(name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                continue;
            }

            sanitized[name] = provider;
        }

        return new Capabilities(currentVersion, updatedAt, sanitized);
    }

    public string? GetSelectedProvider(string name)
    {
        EnsureKnown(name);
        return _selections.TryGetValue(name, out var provider) ? provider : null;
    }

    /// <summary>
    /// Persist a new selected provider for <paramref name="name"/>. Pass <c>null</c> to clear.
    /// Returns <c>true</c> if the stored value changed; a no-op call returns <c>false</c>.
    /// </summary>
    public bool SetSelectedProvider(string name, string? provider, DateTimeOffset now)
    {
        EnsureKnown(name);
        var normalized = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();

        _selections.TryGetValue(name, out var current);
        if (string.Equals(current, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized is null)
        {
            _selections.Remove(name);
        }
        else
        {
            _selections[name] = normalized;
        }

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
