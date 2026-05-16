namespace Throne.Domain.Intents;

internal static class FractionalIndexAppend
{
    public static string After(string before)
    {
        if (before.Length == 0)
        {
            return FractionalIndexAlphabet.MidChar.ToString();
        }
        var lastIdx = FractionalIndexAlphabet.IndexOf(before[^1]);
        if (lastIdx < FractionalIndexAlphabet.Chars.Length - 1)
        {
            var midIdx = (lastIdx + FractionalIndexAlphabet.Chars.Length) / 2;
            return string.Concat(before.AsSpan(0, before.Length - 1), FractionalIndexAlphabet.Chars[midIdx].ToString());
        }
        return before + FractionalIndexAlphabet.MidChar;
    }
}
