namespace Throne.Domain.Intents;

internal static class FractionalIndexValidator
{
    public static void EnsureBounds(string? before, string? after)
    {
        if (before is not null)
        {
            EnsureKey(before, nameof(before));
        }
        if (after is not null)
        {
            EnsureKey(after, nameof(after));
        }
        if (before is not null && after is not null && string.CompareOrdinal(before, after) >= 0)
        {
            throw new ArgumentException(
                $"before ('{before}') must be lexicographically less than after ('{after}').",
                nameof(before));
        }
    }

    public static void EnsureKey(string key, string paramName)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length == 0)
        {
            throw new ArgumentException("Sort key must not be empty.", paramName);
        }
        if (key[^1] == FractionalIndexAlphabet.MinChar)
        {
            throw new ArgumentException(
                $"Sort key must not end in '{FractionalIndexAlphabet.MinChar}' (would leave no room to insert before it).",
                paramName);
        }
        for (var i = 0; i < key.Length; i++)
        {
            _ = FractionalIndexAlphabet.IndexOf(key[i]);
        }
    }
}
