using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

internal sealed partial class AuditingMcpServerTool : DelegatingMcpServerTool
{
    private readonly AIFunction _aiFunction;
    private readonly IMcpCallLogSink _callLogSink;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditingMcpServerTool> _logger;
    private readonly ServerVersion _serverVersion;

    public AuditingMcpServerTool(
        McpServerTool inner,
        AIFunction aiFunction,
        IMcpCallLogSink callLogSink,
        ICurrentUserAccessor currentUser,
        TimeProvider clock,
        ILogger<AuditingMcpServerTool> logger,
        ServerVersion serverVersion)
        : base(inner)
    {
        _aiFunction = aiFunction;
        _callLogSink = callLogSink;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
        _serverVersion = serverVersion;
    }

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        var toolName = request.Params?.Name ?? ProtocolTool.Name;
        var arguments = NormalizeArguments(request.Params?.Arguments);
        var intentId = ExtractIntentId(request.Params?.Arguments);
        var modeHint = ExtractModeHint(toolName, request.Params?.Arguments);
        var sessionId = request.Server.SessionId;
        var userId = _currentUser.UserId;
        var startedAt = _clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        var aiArgs = BuildAIFunctionArguments(request);

        try
        {
            var result = await _aiFunction.InvokeAsync(aiArgs, cancellationToken);
            stopwatch.Stop();

            var callResult = McpToolResultConverter.ToCallToolResult(result, ProtocolTool, _aiFunction.JsonSerializerOptions);
            var outcome = callResult.IsError == true ? McpCallOutcome.Error : McpCallOutcome.Success;
            var errorCode = outcome == McpCallOutcome.Error ? TryReadErrorCode(callResult) : null;
            var errorMessage = outcome == McpCallOutcome.Error ? TryReadErrorMessage(callResult) : null;
            var summary = outcome == McpCallOutcome.Success ? McpResultSummarizer.Summarize(toolName, callResult) : null;

            await TryWriteAuditAsync(
                startedAt, sessionId, userId, toolName, arguments, intentId, modeHint,
                outcome, errorCode, errorMessage, null, summary, (int)stopwatch.ElapsedMilliseconds, cancellationToken);

            return callResult;
        }
        catch (ApiException ex)
        {
            stopwatch.Stop();
            var callResult = McpErrorResultFactory.FromApiException(ex);
            await TryWriteAuditAsync(
                startedAt, sessionId, userId, toolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, ex.Code, ex.Detail, ex.GetType().FullName,
                null, (int)stopwatch.ElapsedMilliseconds, cancellationToken);
            return callResult;
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
                null, (int)stopwatch.ElapsedMilliseconds, cancellationToken);
            LogToolFailure(_logger, toolName, ex);
            return McpErrorResultFactory.Internal(toolName);
        }
    }

    private static AIFunctionArguments BuildAIFunctionArguments(RequestContext<CallToolRequestParams> request)
    {
        var args = new AIFunctionArguments { Services = request.Services };
        if (request.Params?.Arguments is { } argDict)
        {
            foreach (var (key, value) in argDict)
            {
                args[key] = value;
            }
        }
        return args;
    }

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
                _serverVersion.Value);

            await _callLogSink.WriteAsync(entry, ct);
        }
        catch (Exception ex)
        {
            LogAuditFailure(_logger, toolName, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Failed to persist MCP call log for tool {Tool}.")]
    private static partial void LogAuditFailure(ILogger logger, string tool, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Unhandled exception in MCP tool {Tool}.")]
    private static partial void LogToolFailure(ILogger logger, string tool, Exception exception);

    private static Dictionary<string, object?> NormalizeArguments(
        IDictionary<string, JsonElement>? arguments)
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

    private static string? ExtractIntentId(IDictionary<string, JsonElement>? arguments) =>
        arguments is not null && arguments.TryGetValue("intent_id", out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string? ExtractModeHint(string toolName, IDictionary<string, JsonElement>? arguments)
    {
        if (toolName != "get_instruction_bundle" || arguments is null)
        {
            return null;
        }

        return arguments.TryGetValue("mode", out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static string? TryReadErrorCode(CallToolResult result) =>
        TryReadStructuredString(result, "code");

    private static string? TryReadErrorMessage(CallToolResult result) =>
        TryReadStructuredString(result, "message")
        ?? TryReadFirstTextBlock(result);

    private static string? TryReadStructuredString(CallToolResult result, string propertyName)
    {
        if (result.StructuredContent is not { } element || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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
