namespace Throne.Domain.Intents;

internal static class FractionalIndexPrepend
{
    public static string Before(string after)
    {
        var firstIdx = FractionalIndexAlphabet.IndexOf(after[0]);
        if (firstIdx > 1)
        {
            return FractionalIndexAlphabet.Chars[firstIdx / 2].ToString();
        }
        if (firstIdx == 1 && after.Length == 1)
        {
            return string.Concat(FractionalIndexAlphabet.MinChar.ToString(), FractionalIndexAlphabet.MidChar.ToString());
        }
        return FractionalIndexAlphabet.MinChar + Before(after[1..]);
    }
}
