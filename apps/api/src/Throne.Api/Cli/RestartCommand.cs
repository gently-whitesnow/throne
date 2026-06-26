namespace Throne.Api.Cli;

/// <summary>
/// <c>throne restart</c> (and the relaunch behind <c>update --restart</c>): stop the
/// current instance, then start a fresh detached daemon replaying the instance's own
/// persisted launch config (url + resolved host args) so the port/db/workspace it was
/// started with survive a restart, regardless of the flags passed to <c>restart</c>.
/// An explicit <c>-p</c> on this invocation overrides just the port; with no persisted
/// state it falls back to this invocation's args.
/// </summary>
internal static class RestartCommand
{
    public static async Task<int> RunAsync(CliRequest request, CancellationToken ct)
    {
        var previous = DaemonState.TryRead(request.Home);
        await StopCommand.RunAsync(request, ct);
        return await StartCommand.RunAsync(Relaunch(request, previous), ct);
    }

    internal static CliRequest Relaunch(CliRequest request, DaemonState? previous)
    {
        if (previous?.HostArgs is not { Count: > 0 })
        {
            return request with { Attach = false };
        }

        var hostArgs = previous.HostArgs.ToList();
        var url = string.IsNullOrEmpty(previous.Url) ? request.Url : previous.Url;

        if (request.Port is not null)
        {
            // Appended last so it wins over the replayed --urls (ASP.NET: last value per key).
            hostArgs.Add("--urls");
            hostArgs.Add($"http://localhost:{request.Port.Trim()}");
            url = request.Url;
        }

        return request with { Attach = false, Url = url, HostArgs = [.. hostArgs] };
    }
}
