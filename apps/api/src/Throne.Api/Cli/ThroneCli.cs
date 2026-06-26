namespace Throne.Api.Cli;

/// <summary>
/// Entry-point dispatch for the single <c>throne</c> binary. Global flags
/// (<c>--home</c>/<c>-p</c>/<c>--db</c>/<c>-a</c>/<c>--no-browser</c>) are parsed by
/// <see cref="CliRequest"/>; the leading verb selects the command. Bare <c>throne</c>
/// starts (detached on unix, foreground otherwise); <c>serve</c> is the raw Kestrel
/// host the detached child re-enters.
/// </summary>
public static class ThroneCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var request = CliRequest.Parse(args);
        var ct = CancellationToken.None;

        switch (request.Command)
        {
            case CliCommand.Help:
                return CliHelp.Print();

            case CliCommand.Version:
                return CliHelp.PrintVersion();

            case CliCommand.Update:
                return await UpdateCommand.RunAsync(request);

            case CliCommand.Serve:
                DaemonRuntime.BootstrapIfDaemon();
                await ThroneWebHost.RunAsync(request.HostArgs);
                return 0;

            case CliCommand.Stop:
                return await StopCommand.RunAsync(request, ct);

            case CliCommand.Restart:
                return await RestartCommand.RunAsync(request, ct);

            case CliCommand.Status:
                return await StatusCommand.RunAsync(request, ct);

            case CliCommand.Logs:
                return await LogsCommand.RunAsync(request);

            default:
                return await StartCommand.RunAsync(request, ct);
        }
    }
}
