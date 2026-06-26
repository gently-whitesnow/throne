namespace Throne.Api.Cli;

/// <summary>
/// Entry-point dispatch for the single `throne` binary. With no subcommand (or
/// explicit <c>serve</c>) it boots the web host; <c>update</c> self-updates from
/// the latest GitHub release; <c>version</c> prints the build version. Any other
/// leading token (e.g. <c>--urls</c>) is treated as host configuration and passed
/// straight through to <see cref="ThroneWebHost"/>.
/// </summary>
public static class ThroneCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Length > 0 ? args[0] : null;
        switch (command)
        {
            case "update":
                return await UpdateCommand.RunAsync(args[1..]);

            case "version" or "--version" or "-v":
                Console.WriteLine(ThroneVersion.Current);
                return 0;

            case "serve":
                await ThroneWebHost.RunAsync(args[1..]);
                return 0;

            default:
                await ThroneWebHost.RunAsync(args);
                return 0;
        }
    }
}
