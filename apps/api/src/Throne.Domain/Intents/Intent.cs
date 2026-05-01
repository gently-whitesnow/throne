namespace Throne.Domain.Intents;

public sealed class Intent
{
    private readonly List<string> _tags;

    private Intent(
        IntentId id,
        string text,
        int currentVersion,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Text = text;
        CurrentVersion = currentVersion;
        _tags = [.. tags];
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public IntentId Id { get; }
    public string Text { get; private set; }
    public int CurrentVersion { get; private set; }
    public IReadOnlyList<string> Tags => _tags;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Intent Create(
        IntentId id,
        string text,
        IReadOnlyList<string>? tags,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("Intent text must not be empty.", nameof(text));
        }

        var normalizedTags = NormalizeTags(tags);
        return new Intent(id, text, currentVersion: 1, normalizedTags, now, now);
    }

    public static Intent Restore(
        IntentId id,
        string text,
        int currentVersion,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        return new Intent(id, text, currentVersion, tags, createdAt, updatedAt);
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(tags.Count);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
