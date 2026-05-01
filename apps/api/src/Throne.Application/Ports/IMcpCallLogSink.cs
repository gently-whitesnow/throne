namespace Throne.Application.Ports;

public sealed record McpCallLogEntry(
    DateTimeOffset CreatedAt,
    string? SessionId,
    string ToolName,
    IReadOnlyDictionary<string, object?> Arguments,
    string? IntentId,
    string? ModeHint,
    McpCallOutcome Outcome,
    string? ErrorCode,
    IReadOnlyDictionary<string, object?>? ResultSummary,
    int DurationMs,
    string ServerVersion);

public enum McpCallOutcome
{
    Success = 1,
    Error = 2,
}

public interface IMcpCallLogSink
{
    Task WriteAsync(McpCallLogEntry entry, CancellationToken ct);
}
