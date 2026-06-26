namespace Throne.Api.Cli;

/// <summary>
/// <c>throne restart</c>: stop the current instance (if any), then start a fresh
/// detached daemon. Restart never attaches, so the start path always takes the
/// daemon branch (falling back to foreground only where detach is unsupported).
/// </summary>
internal static class RestartCommand
{
    public static async Task<int> RunAsync(CliRequest request, CancellationToken ct)
    {
        await StopCommand.RunAsync(request, ct);
        return await StartCommand.RunAsync(request with { Attach = false }, ct);
    }
}
