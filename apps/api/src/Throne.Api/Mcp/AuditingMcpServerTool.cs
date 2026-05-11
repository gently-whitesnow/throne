using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

internal sealed partial class AuditingMcpServerTool(
    McpServerTool inner,
    IMcpCallLogSink callLogSink,
    ICurrentUserAccessor currentUser,
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
        var sessionId = ExtractSessionId(request);
        var userId = currentUser.UserId;
        var startedAt = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await inner.InvokeAsync(request, cancellationToken);
            stopwatch.Stop();

            var summary = SummarizeResult(toolName, result);
            var outcome = result.IsError == true ? McpCallOutcome.Error : McpCallOutcome.Success;
            var errorCode = outcome == McpCallOutcome.Error ? TryReadErrorCode(result) : null;
            var errorMessage = outcome == McpCallOutcome.Error ? TryReadErrorMessage(result) : null;

            await TryWriteAuditAsync(
                startedAt, sessionId, userId, toolName, arguments, intentId, modeHint,
                outcome, errorCode, errorMessage, null, summary, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                ;

            return result;
        }
        catch (ApiException ex)
        {
            stopwatch.Stop();
            await TryWriteAuditAsync(
                startedAt, sessionId, userId, toolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, ex.Code, ex.Detail, ex.GetType().FullName,
                null, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                ;
            return BuildErrorResult(ex.Code, ex.Detail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Не превращаем cancellation в audit error — обычное завершение.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await TryWriteAuditAsync(
                startedAt, sessionId, userId, toolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, "internal_error", ex.Message, ex.GetType().FullName,
                null, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                ;
            LogToolFailure(logger, toolName, ex);
            return BuildErrorResult("internal_error", ex.Message);
        }
    }

    private static CallToolResult BuildErrorResult(string code, string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
        StructuredContent = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        },
    };

    private async Task TryWriteAuditAsync(
        DateTimeOffset createdAt,
        string? sessionId,
        string? userId,
        string toolName,
        Dictionary<string, object?> arguments,
        string? intentId,
        string? modeHint,
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
                createdAt,
                sessionId,
                userId,
                toolName,
                arguments,
                intentId,
                modeHint,
                outcome,
                errorCode,
                errorMessage,
                exceptionType,
                resultSummary,
                durationMs,
                serverVersion.Value);

            await callLogSink.WriteAsync(entry, ct);
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

    private static string? ExtractSessionId(RequestContext<CallToolRequestParams> request) =>
        request.Server.SessionId;

    private static readonly JsonSerializerOptions SummaryJsonOptions = new(JsonSerializerDefaults.Web);

    private static Dictionary<string, object?>? SummarizeResult(string toolName, CallToolResult result)
    {
        if (result.StructuredContent is null)
        {
            return null;
        }

        if (toolName == "get_instruction_bundle")
        {
            return SummarizeInstructionBundleResult(result);
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

    private static Dictionary<string, object?>? SummarizeInstructionBundleResult(CallToolResult result)
    {
        try
        {
            using var doc = JsonDocument.Parse(result.StructuredContent!.ToJsonString());
            var root = doc.RootElement;
            var instructions = new List<Dictionary<string, object?>>();

            if (root.TryGetProperty("instructions", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    instructions.Add(new Dictionary<string, object?>
                    {
                        ["kind"] = ReadString(item, "kind"),
                        ["instruction_id"] = ReadString(item, "instruction_id"),
                        ["version"] = ReadInt(item, "current_version") ?? ReadInt(item, "version"),
                    });
                }
            }

            return new Dictionary<string, object?>
            {
                ["intent_id"] = ReadString(root, "intent_id"),
                ["mode"] = ReadString(root, "mode"),
                ["instructions"] = instructions,
                ["missing_kinds"] = ReadStringArray(root, "missing_kinds"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : null;

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static string? TryReadErrorCode(CallToolResult result) =>
        TryReadStructuredString(result, "code");

    private static string? TryReadErrorMessage(CallToolResult result) =>
        TryReadStructuredString(result, "message")
        ?? TryReadFirstTextBlock(result);

    private static string? TryReadStructuredString(CallToolResult result, string propertyName)
    {
        if (result.StructuredContent is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StructuredContent.ToJsonString());
            return doc.RootElement.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadFirstTextBlock(CallToolResult result)
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
