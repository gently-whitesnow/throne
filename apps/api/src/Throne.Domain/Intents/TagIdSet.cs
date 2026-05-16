using Throne.Domain.Tags;

namespace Throne.Domain.Intents;

internal static class TagIdSet
{
    public static List<TagId> Normalize(IReadOnlyList<TagId>? tagIds)
    {
        if (tagIds is null || tagIds.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TagId>(tagIds.Count);
        foreach (var id in tagIds)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                continue;
            }

            if (seen.Add(id.Value))
            {
                result.Add(id);
            }
        }

        return result;
    }

    public static bool Equal(IReadOnlyList<TagId> left, IReadOnlyList<TagId> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Value, right[i].Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
