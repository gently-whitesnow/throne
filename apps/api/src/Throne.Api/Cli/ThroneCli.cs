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
    /// <summary>
    /// Lets an in-process host ask for the raw <c>serve</c> path when it cannot pass
    /// a CLI verb. <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
    /// boots <c>Program.Main</c> with no args, which resolves to <c>start</c>; <c>start</c>
    /// records the caller's pid as a live daemon and then refuses every further boot in
    /// the same process, so a test host that boots many factories dies with «The entry
    /// point exited without ever building an IHost». Only an implicit <c>start</c> is
    /// upgraded, so an explicit verb from the real CLI is never touched; unset in normal use.
    /// </summary>
    public const string CommandEnvVar = "THRONE_CLI_COMMAND";

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var request = CliRequest.Parse(args);
        var command = ResolveCommand(request.Command);
        var ct = CancellationToken.None;

        switch (command)
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

    private static CliCommand ResolveCommand(CliCommand parsed) =>
        parsed == CliCommand.Start
            && Environment.GetEnvironmentVariable(CommandEnvVar) == "serve"
            ? CliCommand.Serve
            : parsed;
}
