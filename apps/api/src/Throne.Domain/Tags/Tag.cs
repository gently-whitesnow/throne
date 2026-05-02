namespace Throne.Domain.Tags;

public sealed class Tag
{
    private Tag(TagId id, string name, int currentVersion, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public TagId Id { get; }
    public string Name { get; private set; }
    public int CurrentVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tag Create(TagId id, string rawName, DateTimeOffset now)
    {
        var normalized = TagNames.Normalize(rawName);
        return new Tag(id, normalized, currentVersion: 1, now, now);
    }

    public static Tag Restore(TagId id, string name, int currentVersion, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        if (!TagNames.IsNormalized(name))
        {
            throw new ArgumentException($"Stored tag name '{name}' is not normalized.", nameof(name));
        }

        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        return new Tag(id, name, currentVersion, createdAt, updatedAt);
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
}
