namespace Throne.Domain.Tags;

public static class TagFactory
{
    public static Tag Create(TagId id, string rawName, DateTimeOffset now)
    {
        var normalized = TagNames.Normalize(rawName);
        return new Tag(id, normalized, currentVersion: 1, now, now);
    }

    public static Tag Restore(TagId id, string name, int currentVersion, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        TagGuards.EnsureValidRestore(name, currentVersion);
        return new Tag(id, name, currentVersion, createdAt, updatedAt);
    }
}
