namespace Throne.Domain;

internal static class TextEditLineCount
{
    public static int CountLines(string text)
    {
        var totalLines = text.Length == 0 ? 0 : 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                totalLines++;
            }
        }
        return totalLines;
    }

    public static int FindLineEndOffset(string text, int line1Indexed)
    {
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                seen++;
                if (seen == line1Indexed)
                {
                    return i + 1;
                }
            }
        }
        return text.Length;
    }
}
