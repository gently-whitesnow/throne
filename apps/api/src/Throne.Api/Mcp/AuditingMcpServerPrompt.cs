using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

internal sealed partial class AuditingMcpServerPrompt(
    McpServerPrompt inner,
    IMcpCallLogSink callLogSink,
    TimeProvider clock,
    ILogger<AuditingMcpServerPrompt> logger,
    ServerVersion serverVersion) : DelegatingMcpServerPrompt(inner)
{
    public override async ValueTask<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken)
    {
        var promptName = request.Params?.Name ?? ProtocolPrompt.Name;
        var auditToolName = $"prompts/get:{promptName}";
        var arguments = NormalizeArguments(request.Params?.Arguments);
        var intentId = ExtractIntentId(request.Params?.Arguments);
        string? modeHint = null;
        var sessionId = request.Server.SessionId;
        var startedAt = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await base.GetAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            await TryWriteAuditAsync(
                startedAt, sessionId, auditToolName, arguments, intentId, modeHint,
                McpCallOutcome.Success, errorCode: null, SummarizeResult(result),
                (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (ApiException ex)
        {
            stopwatch.Stop();
            await TryWriteAuditAsync(
                startedAt, sessionId, auditToolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, ex.Code, resultSummary: null,
                (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await TryWriteAuditAsync(
                startedAt, sessionId, auditToolName, arguments, intentId, modeHint,
                McpCallOutcome.Error, "internal_error", resultSummary: null,
                (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            LogPromptFailure(logger, promptName, ex);
            throw;
        }
    }

    private async Task TryWriteAuditAsync(
        DateTimeOffset createdAt,
        string? sessionId,
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
                sessionId,
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
        Message = "Failed to persist MCP call log for prompt {Tool}.")]
    private static partial void LogAuditFailure(ILogger logger, string tool, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Unhandled exception in MCP prompt {Prompt}.")]
    private static partial void LogPromptFailure(ILogger logger, string prompt, Exception exception);

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

    private static Dictionary<string, object?> SummarizeResult(GetPromptResult result)
    {
        var messagesCount = result.Messages?.Count ?? 0;
        var userChars = 0;
        var assistantChars = 0;

        if (result.Messages is not null)
        {
            foreach (var message in result.Messages)
            {
                var len = message.Content is TextContentBlock text ? text.Text.Length : 0;
                if (message.Role == Role.Assistant)
                {
                    assistantChars += len;
                }
                else
                {
                    userChars += len;
                }
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["messages_count"] = messagesCount,
            ["user_chars"] = userChars,
            ["assistant_chars"] = assistantChars,
        };
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
