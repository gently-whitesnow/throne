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
/// </summary>
public static class FractionalIndex
{
    public const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private const char MinChar = '0';
    private static readonly char MidChar = Alphabet[Alphabet.Length / 2];

    public static string Initial() => MidChar.ToString();

    public static string Between(string? before, string? after)
    {
        EnsureBounds(before, after);
        return (before, after) switch
        {
            (null, null) => Initial(),
            (null, _) => Prepend(after!),
            (_, null) => Append(before),
            _ => Midpoint(before, after),
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
            keys.Add(Append(keys[i - 1]));
        }
        return keys;
    }

    public static void ValidateKey(string key, string paramName = "key")
        => EnsureKey(key, paramName);

    private static int IndexOf(char c)
    {
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

    private static string Append(string before)
    {
        if (before.Length == 0)
        {
            return MidChar.ToString();
        }
        var lastIdx = IndexOf(before[^1]);
        if (lastIdx < Alphabet.Length - 1)
        {
            var midIdx = (lastIdx + Alphabet.Length) / 2;
            return string.Concat(before.AsSpan(0, before.Length - 1), Alphabet[midIdx].ToString());
        }
        return before + MidChar;
    }

    private static string Prepend(string after)
    {
        var firstIdx = IndexOf(after[0]);
        if (firstIdx > 1)
        {
            return Alphabet[firstIdx / 2].ToString();
        }
        if (firstIdx == 1 && after.Length == 1)
        {
            return string.Concat(MinChar.ToString(), MidChar.ToString());
        }
        return MinChar + Prepend(after[1..]);
    }

    private static string Midpoint(string a, string b)
    {
        var n = SharedPrefixLength(a, b);
        var prefix = a[..n];
        var av = n < a.Length ? IndexOf(a[n]) : 0;
        var bv = n < b.Length ? IndexOf(b[n]) : 0;
        if (bv - av > 1)
        {
            return prefix + Alphabet[(av + bv) / 2];
        }
        var aRest = n + 1 < a.Length ? a[(n + 1)..] : string.Empty;
        return string.Concat(prefix, Alphabet[av].ToString(), Append(aRest));
    }

    private static int SharedPrefixLength(string a, string b)
    {
        var n = 0;
        while (true)
        {
            var ca = n < a.Length ? a[n] : MinChar;
            var cb = n < b.Length ? b[n] : MinChar;
            if (ca != cb)
            {
                return n;
            }
            n++;
        }
    }

    private static void EnsureBounds(string? before, string? after)
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

    private static void EnsureKey(string key, string paramName)
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
