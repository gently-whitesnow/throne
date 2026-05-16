namespace Throne.Domain;

internal static class TextEditMatcher
{
    public static List<int> FindAllIndices(string haystack, string needle)
    {
        var result = new List<int>();
        var from = 0;
        while (true)
        {
            var idx = haystack.IndexOf(needle, from, StringComparison.Ordinal);
            if (idx < 0)
            {
                break;
            }
            result.Add(idx);
            from = idx + needle.Length;
            if (needle.Length == 0)
            {
                break;
            }
        }
        return result;
    }

    public static string BuildQueryPreview(string oldText)
    {
        const int max = 80;
        return oldText.Length <= max ? oldText : oldText[..max];
    }
}
