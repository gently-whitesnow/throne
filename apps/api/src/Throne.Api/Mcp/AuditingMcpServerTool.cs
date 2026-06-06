using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Api.Mcp.Tools;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

internal sealed partial class AuditingMcpServerTool : DelegatingMcpServerTool
{
    private readonly AIFunction _aiFunction;
    private readonly McpAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditingMcpServerTool> _logger;

    public AuditingMcpServerTool(
        McpServerTool inner,
        AIFunction aiFunction,
        IMcpCallLogSink callLogSink,
        TimeProvider clock,
        ILogger<AuditingMcpServerTool> logger,
        ServerVersion serverVersion)
        : base(inner)
    {
        _aiFunction = aiFunction;
        _audit = new McpAuditLogger(callLogSink, logger, serverVersion);
        _clock = clock;
        _logger = logger;
    }

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        var fields = CaptureFields(request);
        var stopwatch = Stopwatch.StartNew();
        var aiArgs = McpToolArgumentBinder.Build(_aiFunction, request);

        try
        {
            var result = await _aiFunction.InvokeAsync(aiArgs, cancellationToken);
            stopwatch.Stop();

            // ADR-0003 §8.1: prompt-like tools return McpToolPayload — wire goes out as-is
            // (StructuredContent already null), audit summary travels through the OOB envelope.
            IReadOnlyDictionary<string, object?>? overrideSummary = null;
            if (result is McpToolPayload payload)
            {
                overrideSummary = payload.AuditSummary;
                result = payload.Wire;
            }

            var callResult = McpToolResultConverter.ToCallToolResult(result, ProtocolTool, _aiFunction.JsonSerializerOptions);
            await _audit.WriteFromCallResultAsync(fields, callResult, stopwatch.ElapsedMilliseconds, overrideSummary, cancellationToken);
            return callResult;
        }
        catch (ApiException ex)
        {
            stopwatch.Stop();
            var callResult = McpErrorResultFactory.FromApiException(ex);
            await _audit.WriteErrorAsync(fields, ex.Code, ex.Detail, ex.GetType().FullName, stopwatch.ElapsedMilliseconds, cancellationToken);
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
            await _audit.WriteErrorAsync(fields, "internal_error", ex.Message, ex.GetType().FullName, stopwatch.ElapsedMilliseconds, cancellationToken);
            LogToolFailure(_logger, fields.ToolName, ex);
            return McpErrorResultFactory.Internal(fields.ToolName, ex.Message);
        }
    }

    private McpCallFields CaptureFields(RequestContext<CallToolRequestParams> request)
    {
        var toolName = request.Params?.Name ?? ProtocolTool.Name;
        return new McpCallFields(
            toolName,
            McpCallArgumentSnapshot.Normalize(request.Params?.Arguments),
            McpCallArgumentSnapshot.ExtractIntentId(request.Params?.Arguments),
            McpCallArgumentSnapshot.ExtractModeHint(toolName, request.Params?.Arguments),
            request.Server.SessionId,
            _clock.GetUtcNow());
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Unhandled exception in MCP tool {Tool}.")]
    private static partial void LogToolFailure(ILogger logger, string tool, Exception exception);
}

internal sealed record McpCallFields(
    string ToolName,
    Dictionary<string, object?> Arguments,
    string? IntentId,
    string? ModeHint,
    string? SessionId,
    DateTimeOffset StartedAt);
