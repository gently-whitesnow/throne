using Throne.Domain.Tags;

namespace Throne.Domain.Intents;

public static class IntentTagOperation
{
    public static bool SetTagIds(Intent intent, IReadOnlyList<TagId> tagIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(tagIds);
        var normalized = TagIdSet.Normalize(tagIds);
        if (TagIdSet.Equal(intent.TagIds, normalized))
        {
            return false;
        }

        intent.ReplaceTagIds(normalized);
        intent.State = intent.State with { UpdatedAt = now };
        return true;
    }
}
