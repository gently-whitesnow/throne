using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

internal sealed partial class AuditingMcpServerTool(
    McpServerTool inner,
    IMcpCallLogSink callLogSink,
    TimeProvider clock,
    ILogger<AuditingMcpServerTool> logger,
    ServerVersion serverVersion) : McpServerTool
{
    public override Tool ProtocolTool => inner.ProtocolTool;

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        var toolName = request.Params?.Name ?? inner.ProtocolTool.Name;
        var arguments = NormalizeArguments(request.Params?.Arguments);
        var intentId = ExtractIntentId(request.Params?.Arguments);
        var modeHint = ExtractModeHint(toolName, request.Params?.Arguments);
        var startedAt = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await inner.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var summary = SummarizeResult(result);
            var outcome = result.IsError == true ? McpCallOutcome.Error : McpCallOutcome.Success;
            var errorCode = outcome == McpCallOutcome.Error ? TryReadErrorCode(result) : null;

            await TryWriteAuditAsync(
                startedAt, toolName, arguments, intentId, modeHint,
                outcome, errorCode, summary, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (ApiException ex)
        {
            stopwatch.Stop();
            await TryWriteAuditAsync(
                startedAt, toolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, ex.Code, null, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await TryWriteAuditAsync(
                startedAt, toolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, "internal_error", null, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            LogToolFailure(logger, toolName, ex);
            throw;
        }
    }

    private async Task TryWriteAuditAsync(
        DateTimeOffset createdAt,
        string toolName,
        Dictionary<string, object?> arguments,
        string? intentId,
        string? modeHint,
        McpCallOutcome outcome,
        string? errorCode,
        Dictionary<string, object?>? resultSummary,
        int durationMs,
        CancellationToken ct)
    {
        try
        {
            var entry = new McpCallLogEntry(
                createdAt,
                SessionId: null,
                toolName,
                arguments,
                intentId,
                modeHint,
                outcome,
                errorCode,
                resultSummary,
                durationMs,
                serverVersion.Value);

            await callLogSink.WriteAsync(entry, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogAuditFailure(logger, toolName, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Failed to persist MCP call log for tool {Tool}.")]
    private static partial void LogAuditFailure(ILogger logger, string tool, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Unhandled exception in MCP tool {Tool}.")]
    private static partial void LogToolFailure(ILogger logger, string tool, Exception exception);

    private static Dictionary<string, object?> NormalizeArguments(
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var dict = new Dictionary<string, object?>(arguments.Count, StringComparer.Ordinal);
        foreach (var (key, value) in arguments)
        {
            dict[key] = JsonElementToObject(value);
        }

        return dict;
    }

    private static string? ExtractIntentId(IReadOnlyDictionary<string, JsonElement>? arguments) =>
        arguments is not null && arguments.TryGetValue("intent_id", out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string? ExtractModeHint(string toolName, IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        if (toolName != "get_instruction_bundle" || arguments is null)
        {
            return null;
        }

        return arguments.TryGetValue("mode", out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static readonly JsonSerializerOptions SummaryJsonOptions = new(JsonSerializerDefaults.Web);

    private static Dictionary<string, object?>? SummarizeResult(CallToolResult result)
    {
        if (result.StructuredContent is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(
                result.StructuredContent.ToJsonString(),
                SummaryJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadErrorCode(CallToolResult result)
    {
        if (result.StructuredContent is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StructuredContent.ToJsonString());
            return doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };
}
