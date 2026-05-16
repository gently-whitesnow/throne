using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp;

internal static class McpTextContentReader
{
    public static string? TryReadFirst(CallToolResult result)
    {
        if (result.Content is null)
        {
            return null;
        }

        foreach (var block in result.Content)
        {
            if (block is TextContentBlock text && !string.IsNullOrWhiteSpace(text.Text))
            {
                return text.Text;
            }
        }
        return null;
    }
}
