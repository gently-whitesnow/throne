namespace Throne.Domain;

internal static class TextEditLineLookup
{
    public static List<int> ToMatchLines(string text, List<int> indices, int limit)
    {
        var result = new List<int>(Math.Min(indices.Count, limit));
        for (var i = 0; i < indices.Count && result.Count < limit; i++)
        {
            result.Add(LineNumberAt(text, indices[i]));
        }
        return result;
    }

    public static int LineNumberAt(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }
        return line;
    }
}
