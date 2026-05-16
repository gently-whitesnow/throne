using System.Text;

namespace Throne.Domain.Intents;

internal static class IntentTextSearchHelpers
{
    public static int[] ComputeLineStartOffsets(string text)
    {
        var offsets = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                offsets.Add(i + 1);
            }
        }
        return [.. offsets];
    }

    public static (int Line, int Column) LineAndColumn(int[] lineStartOffsets, int index)
    {
        var lo = 0;
        var hi = lineStartOffsets.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (lineStartOffsets[mid] <= index)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return (lo + 1, index - lineStartOffsets[lo] + 1);
    }

    public static string JoinLines(string[] lines, int startLine1Indexed, int endLine1Indexed)
    {
        if (startLine1Indexed > endLine1Indexed)
        {
            return string.Empty;
        }
        var sb = new StringBuilder();
        for (var i = startLine1Indexed - 1; i <= endLine1Indexed - 1; i++)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }
            sb.Append(lines[i]);
        }
        return sb.ToString();
    }
}
