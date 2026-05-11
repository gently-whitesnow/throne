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
/// Mirror this implementation if you ever add a TypeScript counterpart — both sides
/// must agree on the alphabet and the trailing-min-char ban.
/// </summary>
public static class FractionalIndex
{
    public const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private const char MinChar = '0';
    private const char MaxChar = 'z';

    // Median of the alphabet — useful as the very first key and for unbounded picks.
    private static readonly char MidChar = Alphabet[Alphabet.Length / 2];

    public static string Initial() => MidChar.ToString();

    /// <summary>
    /// Returns a key strictly between <paramref name="before"/> and <paramref name="after"/>.
    /// Pass null for an unbounded side: <c>Between(null, x)</c> prepends before x,
    /// <c>Between(x, null)</c> appends after x, <c>Between(null, null)</c> returns the initial key.
    /// </summary>
    public static string Between(string? before, string? after)
    {
        if (before is not null)
        {
            ValidateKey(before, nameof(before));
        }
        if (after is not null)
        {
            ValidateKey(after, nameof(after));
        }
        if (before is not null && after is not null && string.CompareOrdinal(before, after) >= 0)
        {
            throw new ArgumentException(
                $"before ('{before}') must be lexicographically less than after ('{after}').",
                nameof(before));
        }

        if (before is null && after is null)
        {
            return Initial();
        }
        if (before is null)
        {
            return PrependBefore(after!);
        }
        if (after is null)
        {
            return AppendAfter(before);
        }
        return Midpoint(before, after);
    }

    /// <summary>
    /// Generates <paramref name="count"/> strictly-increasing keys starting with the initial key.
    /// Used by the migration backfill to assign even keys to existing rows.
    /// </summary>
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
            keys.Add(AppendAfter(keys[i - 1]));
        }
        return keys;
    }

    private static string PrependBefore(string after)
    {
        // Walk leading chars: any leading run of '0' must be preserved (we cannot go
        // smaller within them), and we recurse into the suffix. Once we reach a digit
        // > '0', either pick a midpoint (if there is room) or recurse with '0' prefix.
        var firstIdx = IndexOf(after[0]);
        if (firstIdx > 1)
        {
            var midIdx = firstIdx / 2;
            return Alphabet[midIdx].ToString();
        }
        if (firstIdx == 1)
        {
            // after[0] == '1'. We need x < after, not ending in MinChar.
            // x = '0' + something < after[1..] (effectively empty, which would mean
            // x <= '0' alone — invalid). So extend with the median.
            if (after.Length == 1)
            {
                return string.Concat(MinChar.ToString(), MidChar.ToString());
            }
            return MinChar + PrependBefore(after[1..]);
        }
        // firstIdx == 0: after starts with MinChar. By validation, after has length >= 2
        // (no trailing MinChar), so recurse.
        return MinChar + PrependBefore(after[1..]);
    }

    private static string AppendAfter(string before)
    {
        if (before.Length == 0)
        {
            return Initial();
        }
        var lastChar = before[^1];
        var lastIdx = IndexOf(lastChar);
        if (lastIdx < Alphabet.Length - 1)
        {
            // Pick a char strictly greater than the last char by going halfway to MaxChar.
            // Half-way always lands above lastIdx because (lastIdx + Alphabet.Length) / 2 > lastIdx
            // whenever lastIdx < Alphabet.Length - 1.
            var midIdx = (lastIdx + Alphabet.Length) / 2;
            return string.Concat(before.AsSpan(0, before.Length - 1), Alphabet[midIdx].ToString());
        }
        // Last char is MaxChar — appending another char keeps the key > before and
        // ensures the new key does not end in MinChar.
        return before + MidChar;
    }

    private static string Midpoint(string a, string b)
    {
        // Walk the common prefix.
        var n = 0;
        while (true)
        {
            var ca = n < a.Length ? a[n] : MinChar;
            var cb = n < b.Length ? b[n] : MinChar;
            if (ca != cb)
            {
                break;
            }
            n++;
        }

        var prefix = a[..n];
        var av = n < a.Length ? IndexOf(a[n]) : 0; // virtual MinChar past end of a
        var bv = n < b.Length ? IndexOf(b[n]) : 0;
        // Since a < b lexicographically and the prefix matched up to n, av < bv.
        if (bv - av > 1)
        {
            var midIdx = (av + bv) / 2;
            return prefix + Alphabet[midIdx];
        }
        // bv == av + 1: we must descend on the a side. Any string starting with
        // prefix + Alphabet[av] is < prefix + Alphabet[bv] <= b (and starts with
        // matching prefix), so we just need it strictly greater than a.
        var aRest = n + 1 < a.Length ? a[(n + 1)..] : string.Empty;
        return string.Concat(prefix, Alphabet[av].ToString(), AppendAfter(aRest));
    }

    private static int IndexOf(char c)
    {
        // Direct mapping over the base62 alphabet: '0..9' = 0..9, 'A..Z' = 10..35, 'a..z' = 36..61.
        var idx = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'Z' => 10 + (c - 'A'),
            >= 'a' and <= 'z' => 36 + (c - 'a'),
            _ => -1,
        };
        if (idx < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(c), $"Char '{c}' is not in the base62 alphabet.");
        }
        return idx;
    }

    public static void ValidateKey(string key, string paramName = "key")
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length == 0)
        {
            throw new ArgumentException("Sort key must not be empty.", paramName);
        }
        if (key[^1] == MinChar)
        {
            throw new ArgumentException(
                $"Sort key must not end in '{MinChar}' (would leave no room to insert before it).",
                paramName);
        }
        for (var i = 0; i < key.Length; i++)
        {
            _ = IndexOf(key[i]);
        }
    }
}
