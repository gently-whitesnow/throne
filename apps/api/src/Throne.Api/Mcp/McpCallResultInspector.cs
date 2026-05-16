using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp;

internal static class McpCallResultInspector
{
    public static string? TryReadErrorCode(CallToolResult result) =>
        McpStructuredResultReader.TryReadString(result, "code");

    public static string? TryReadErrorMessage(CallToolResult result) =>
        McpStructuredResultReader.TryReadString(result, "message")
        ?? McpTextContentReader.TryReadFirst(result);
}
