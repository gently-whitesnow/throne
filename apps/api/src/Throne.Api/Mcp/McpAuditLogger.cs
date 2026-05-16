using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

internal sealed partial class McpAuditLogger(
    IMcpCallLogSink sink,
    ILogger<AuditingMcpServerTool> logger,
    ServerVersion serverVersion)
{
    public Task WriteFromCallResultAsync(
        McpCallFields fields, CallToolResult callResult, long elapsedMs, CancellationToken ct)
    {
        var outcome = callResult.IsError == true ? McpCallOutcome.Error : McpCallOutcome.Success;
        var errorCode = outcome == McpCallOutcome.Error ? McpCallResultInspector.TryReadErrorCode(callResult) : null;
        var errorMessage = outcome == McpCallOutcome.Error ? McpCallResultInspector.TryReadErrorMessage(callResult) : null;
        var summary = outcome == McpCallOutcome.Success ? McpResultSummarizer.Summarize(fields.ToolName, callResult) : null;

        return TryWriteAsync(fields, outcome, errorCode, errorMessage, null, summary, (int)elapsedMs, ct);
    }

    public Task WriteErrorAsync(
        McpCallFields fields,
        string errorCode,
        string? errorMessage,
        string? exceptionType,
        long elapsedMs,
        CancellationToken ct) =>
        TryWriteAsync(fields, McpCallOutcome.Error, errorCode, errorMessage, exceptionType, null, (int)elapsedMs, ct);

    private async Task TryWriteAsync(
        McpCallFields fields,
        McpCallOutcome outcome,
        string? errorCode,
        string? errorMessage,
        string? exceptionType,
        Dictionary<string, object?>? resultSummary,
        int durationMs,
        CancellationToken ct)
    {
        try
        {
            var entry = new McpCallLogEntry(
                fields.StartedAt,
                fields.SessionId,
                fields.UserId,
                fields.ToolName,
                fields.Arguments,
                fields.IntentId,
                fields.ModeHint,
                outcome,
                errorCode,
                errorMessage,
                exceptionType,
                resultSummary,
                durationMs,
                serverVersion.Value);

            await sink.WriteAsync(entry, ct);
        }
        catch (Exception ex)
        {
            LogAuditFailure(logger, fields.ToolName, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Failed to persist MCP call log for tool {Tool}.")]
    private static partial void LogAuditFailure(ILogger logger, string tool, Exception exception);
}
