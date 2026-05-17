namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Pure helpers shared by the Intent MCP tools. Stateless string/collection
/// utilities only — anything that depends on a repository or other DI service
/// lives in a separate instance class (e.g. <see cref="IntentToolTagRefs"/>).
/// </summary>
internal static class IntentToolHelpers
{
    public static string BuildPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            return trimmed.Length <= 200 ? trimmed : trimmed[..200];
        }
        return string.Empty;
    }
}
