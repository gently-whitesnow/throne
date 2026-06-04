namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Maps the first task that completes to a close-code/reason pair from the yaml
/// contract surface (<see cref="WebSocketCloseCodes"/>).
/// </summary>
internal static class TerminalBridgeShutdown
{
    public static ShutdownReason Classify(Task finished, Task inbound, Task outbound, Task watcher)
    {
        if (ReferenceEquals(finished, watcher))
        {
            return new ShutdownReason(WebSocketCloseCodes.NormalClosure, "session_ended");
        }
        if (ReferenceEquals(finished, inbound))
        {
            return new ShutdownReason(WebSocketCloseCodes.NormalClosure, "client_disconnected");
        }
        if (ReferenceEquals(finished, outbound))
        {
            return new ShutdownReason(WebSocketCloseCodes.InternalError, "pipe_closed");
        }
        return new ShutdownReason(WebSocketCloseCodes.InternalError, "unknown");
    }

    public static async Task SilenceAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        catch
        {
            // bridge shutdown is best-effort; the close code already carries the failure
        }
    }
}

internal readonly record struct ShutdownReason(int CloseCode, string Reason);
