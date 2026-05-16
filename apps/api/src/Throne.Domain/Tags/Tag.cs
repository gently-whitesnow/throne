namespace Throne.Domain.Tags;

public sealed class Tag
{
    internal Tag(TagId id, string name, int currentVersion, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public TagId Id { get; }
    public string Name { get; internal set; }
    public int CurrentVersion { get; internal set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; internal set; }

    public bool Rename(string rawName, DateTimeOffset now)
        => TagRenameOperation.Apply(this, rawName, now);
}
