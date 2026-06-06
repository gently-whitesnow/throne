using ModelContextProtocol.Protocol;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

/// <summary>
/// Pure resolver that maps a <see cref="CallToolResult"/> + optional OOB audit
/// summary into the fields persisted by <see cref="McpAuditLogger"/>.
///
/// Prompt-like tools (ADR-0003 §8.1) ship refs via <paramref name="overrideSummary"/>
/// because their wire StructuredContent is null. Structured-data tools leave the
/// override null and we fall back to deserialising the typed StructuredContent
/// from the wire result.
/// </summary>
internal static class McpAuditResolution
{
    public static (McpCallOutcome Outcome, string? ErrorCode, string? ErrorMessage, Dictionary<string, object?>? Summary)
        Resolve(CallToolResult callResult, IReadOnlyDictionary<string, object?>? overrideSummary)
    {
        if (callResult.IsError == true)
        {
            return (
                McpCallOutcome.Error,
                McpCallResultInspector.TryReadErrorCode(callResult),
                McpCallResultInspector.TryReadErrorMessage(callResult),
                null);
        }
        return (McpCallOutcome.Success, null, null, ResolveSummary(callResult, overrideSummary));
    }

    private static Dictionary<string, object?>? ResolveSummary(
        CallToolResult callResult,
        IReadOnlyDictionary<string, object?>? overrideSummary)
    {
        if (overrideSummary is null)
        {
            return McpResultSummarizer.Summarize(callResult);
        }
        return overrideSummary as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(overrideSummary, StringComparer.Ordinal);
    }
}
