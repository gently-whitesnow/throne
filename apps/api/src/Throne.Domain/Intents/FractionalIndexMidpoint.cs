namespace Throne.Domain.Intents;

internal static class FractionalIndexMidpoint
{
    public static string Between(string a, string b)
    {
        var n = SharedPrefixLength(a, b);
        var prefix = a[..n];
        var av = n < a.Length ? FractionalIndexAlphabet.IndexOf(a[n]) : 0;
        var bv = n < b.Length ? FractionalIndexAlphabet.IndexOf(b[n]) : 0;
        if (bv - av > 1)
        {
            return prefix + FractionalIndexAlphabet.Chars[(av + bv) / 2];
        }
        var aRest = n + 1 < a.Length ? a[(n + 1)..] : string.Empty;
        return string.Concat(prefix, FractionalIndexAlphabet.Chars[av].ToString(), FractionalIndexAppend.After(aRest));
    }

    private static int SharedPrefixLength(string a, string b)
    {
        var n = 0;
        while (true)
        {
            var ca = n < a.Length ? a[n] : FractionalIndexAlphabet.MinChar;
            var cb = n < b.Length ? b[n] : FractionalIndexAlphabet.MinChar;
            if (ca != cb)
            {
                return n;
            }
            n++;
        }
    }
}
