using Microsoft.Extensions.Logging;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

/// <summary>
/// LoggerMessage source-generator host for the embedded-terminal WebSocket endpoint.
/// Required because the API project compiles with CA1848 enabled — direct
/// <c>ILogger.LogWarning(...)</c> calls are blocking.
/// </summary>
internal static partial class TerminalEndpointLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Terminal WebSocket bridge for intent {IntentId} aborted unexpectedly.")]
    public static partial void BridgeAborted(ILogger logger, string intentId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Received terminal hook {Event} for intent {IntentId}.")]
    public static partial void HookReceived(ILogger logger, string intentId, Event @event);
}
