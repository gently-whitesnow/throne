namespace Throne.Domain.Tags;

public sealed class Tag
{
    private static readonly IReadOnlyList<TagDefaultRepository> EmptyDefaults = [];

    private Tag(
        TagId id,
        string name,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IReadOnlyList<TagDefaultRepository> defaultRepositories)
    {
        Id = id;
        Name = name;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DefaultRepositories = defaultRepositories;
    }

    public TagId Id { get; }
    public string Name { get; private set; }
    public int CurrentVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Repository presets unioned by the Slice 2 Run pre-flight across all tags of an intent.
    /// Order matches the last persisted state — callers must treat it as a set keyed on
    /// <see cref="TagDefaultRepository.Coordinate"/>. The aggregate enforces dedup on writes.
    /// </summary>
    public IReadOnlyList<TagDefaultRepository> DefaultRepositories { get; private set; }

    public static Tag Create(TagId id, string rawName, DateTimeOffset now)
    {
        var normalized = TagNames.Normalize(rawName);
        return new Tag(id, normalized, currentVersion: 1, now, now, EmptyDefaults);
    }

    public static Tag Restore(
        TagId id,
        string name,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IReadOnlyList<TagDefaultRepository>? defaultRepositories = null)
    {
        if (!TagNames.IsNormalized(name))
        {
            throw new ArgumentException($"Stored tag name '{name}' is not normalized.", nameof(name));
        }

        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        var defaults = defaultRepositories is null or { Count: 0 }
            ? EmptyDefaults
            : EnsureDeduped(defaultRepositories, nameof(defaultRepositories));

        return new Tag(id, name, currentVersion, createdAt, updatedAt, defaults);
    }

    public bool Rename(string rawName, DateTimeOffset now)
    {
        var normalized = TagNames.Normalize(rawName);
        if (string.Equals(Name, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        Name = normalized;
        CurrentVersion += 1;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Replace the whole <see cref="DefaultRepositories"/> collection. Returns
    /// <c>true</c> when the resulting set differs from the current one (and bumps
    /// <see cref="CurrentVersion"/>); otherwise the call is a no-op so callers can
    /// safely send a PUT with the existing list. Uniqueness on
    /// <see cref="TagDefaultRepository.Coordinate"/> is enforced inside the aggregate.
    /// </summary>
    public bool ReplaceDefaultRepositories(
        IReadOnlyList<TagDefaultRepository> repositories,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var normalized = repositories.Count == 0
            ? EmptyDefaults
            : EnsureDeduped(repositories, nameof(repositories));

        if (SameAsCurrent(normalized))
        {
            return false;
        }

        DefaultRepositories = normalized;
        CurrentVersion += 1;
        UpdatedAt = now;
        return true;
    }

    private bool SameAsCurrent(IReadOnlyList<TagDefaultRepository> candidate)
    {
        if (DefaultRepositories.Count != candidate.Count)
        {
            return false;
        }

        for (var i = 0; i < candidate.Count; i++)
        {
            if (!DefaultRepositories[i].Equals(candidate[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static List<TagDefaultRepository> EnsureDeduped(
        IReadOnlyList<TagDefaultRepository> source,
        string parameterName)
    {
        var seen = new HashSet<(string Provider, string Owner, string Repo)>(source.Count);
        var result = new List<TagDefaultRepository>(source.Count);
        foreach (var entry in source)
        {
            if (entry is null)
            {
                throw new ArgumentException(
                    "default_repositories entries must not be null.",
                    parameterName);
            }

            var key = (entry.Coordinate.Provider, entry.Coordinate.Owner, entry.Coordinate.Repo);
            if (!seen.Add(key))
            {
                throw new ArgumentException(
                    $"default_repositories must be unique on (provider, owner, repo); duplicate {entry.Coordinate.FullName}.",
                    parameterName);
            }

            result.Add(entry);
        }

        return result;
    }
}
