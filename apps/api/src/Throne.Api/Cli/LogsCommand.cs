namespace Throne.Api.Cli;

/// <summary>
/// <c>throne logs [-f]</c>: prints the tail of the current home's daemon log and,
/// with <c>-f</c>/<c>--follow</c>, streams new lines until Ctrl-C.
/// </summary>
internal static class LogsCommand
{
    private const int TailLines = 200;

    public static async Task<int> RunAsync(CliRequest request)
    {
        var path = request.Home.LogFile;
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"No logs yet at {path}. Start throne first.");
            return 1;
        }

        foreach (var line in LogReader.LastLines(path, TailLines))
        {
            Console.WriteLine(line);
        }

        if (!request.Follow)
        {
            return 0;
        }

        using var cts = new CancellationTokenSource();
        ConsoleInterrupt.LinkTo(cts);
        try
        {
            await LogReader.FollowAsync(path, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }
}
