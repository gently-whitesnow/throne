namespace Throne.Domain.Intents;

/// <summary>
/// Fractional indexing over a base62 alphabet (LexoRank-style). A key is a non-empty
/// string whose lexicographic order defines the position in a list. Between any two
/// keys we can always synthesize a strictly-greater-than-A and strictly-less-than-B
/// midpoint, so reorder operations never require global rebalancing.
///
/// Invariants:
///   - alphabet is "0..9A..Za..z" (62 chars, lexicographically ordered).
///   - keys are non-empty.
///   - keys never end in the minimum char ('0'), so we always have room to subdivide.
///
/// Branch-heavy helpers live in <see cref="FractionalIndexAppend"/> /
/// <see cref="FractionalIndexPrepend"/> / <see cref="FractionalIndexMidpoint"/> /
/// <see cref="FractionalIndexAlphabet"/> / <see cref="FractionalIndexValidator"/>
/// so this façade stays under the per-type CA1502 budget.
/// </summary>
public static class FractionalIndex
{
    public const string Alphabet = FractionalIndexAlphabet.Chars;

    public static string Initial() => FractionalIndexAlphabet.MidChar.ToString();

    public static string Between(string? before, string? after)
    {
        FractionalIndexValidator.EnsureBounds(before, after);
        return (before, after) switch
        {
            (null, null) => Initial(),
            (null, _) => FractionalIndexPrepend.Before(after!),
            (_, null) => FractionalIndexAppend.After(before),
            _ => FractionalIndexMidpoint.Between(before, after),
        };
    }

    public static IReadOnlyList<string> GenerateAscending(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return [];
        }
        var keys = new List<string>(count) { Initial() };
        for (var i = 1; i < count; i++)
        {
            keys.Add(FractionalIndexAppend.After(keys[i - 1]));
        }
        return keys;
    }

    public static void ValidateKey(string key, string paramName = "key")
        => FractionalIndexValidator.EnsureKey(key, paramName);
}
