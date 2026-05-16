using ModelContextProtocol.Protocol;

namespace Throne.Mcp.Stdio;

internal static class UpstreamToolSnapshotComparer
{
    public static bool ToolsEquivalent(Tool[] a, Tool[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (!ToolEquivalent(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ToolEquivalent(Tool a, Tool b) =>
        string.Equals(a.Name, b.Name, StringComparison.Ordinal)
        && a.InputSchema.GetRawText() == b.InputSchema.GetRawText();
}
