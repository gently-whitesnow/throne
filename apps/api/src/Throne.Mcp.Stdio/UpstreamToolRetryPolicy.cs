using ModelContextProtocol.Protocol;

namespace Throne.Mcp.Stdio;

internal static class UpstreamToolRetryPolicy
{
    public static bool CanRetry(string toolName, IReadOnlyList<Tool> tools)
    {
        var tool = tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal));
        return tool?.Annotations?.ReadOnlyHint == true
            || tool?.Annotations?.IdempotentHint == true;
    }
}
