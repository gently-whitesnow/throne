namespace Throne.Domain.Tags;

internal static class TagRenameOperation
{
    public static bool Apply(Tag tag, string rawName, DateTimeOffset now)
    {
        var normalized = TagNames.Normalize(rawName);
        if (string.Equals(tag.Name, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        tag.Name = normalized;
        tag.CurrentVersion += 1;
        tag.UpdatedAt = now;
        return true;
    }
}
