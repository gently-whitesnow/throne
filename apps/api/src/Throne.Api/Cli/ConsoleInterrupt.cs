namespace Throne.Api.Cli;

/// <summary>
/// Turns Ctrl-C into a cancellation instead of an abrupt process kill, so a
/// foreground command (e.g. <c>logs -f</c>) can unwind cleanly on the first signal.
/// </summary>
internal static class ConsoleInterrupt
{
    public static void LinkTo(CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
    }
}
