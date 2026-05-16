namespace Throne.Domain.Intents;

internal static class FractionalIndexAlphabet
{
    public const string Chars =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public const char MinChar = '0';
    public const char MaxChar = 'z';
    public static readonly char MidChar = Chars[Chars.Length / 2];

    public static int IndexOf(char c)
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
}
