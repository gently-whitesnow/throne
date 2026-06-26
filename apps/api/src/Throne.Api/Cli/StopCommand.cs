namespace Throne.Api.Cli;

/// <summary>
/// <c>throne stop</c>: reads the current home's pid, asks the instance to shut down
/// gracefully (SIGTERM → SIGKILL fallback), then clears the pid/state. A missing or
/// stale pidfile is reported as "not running", not an error.
/// </summary>
internal static class StopCommand
{
    public static async Task<int> RunAsync(CliRequest request, CancellationToken ct)
    {
        var home = request.Home;
        var state = DaemonState.TryRead(home);

        if (state is null || !state.IsAlive)
        {
            if (state is not null)
            {
                DaemonState.Clear(home);
            }

            Console.WriteLine($"throne is not running (home {home.Directory}).");
            return 0;
        }

        Console.WriteLine($"Stopping throne (pid {state.Pid}) …");
        await DaemonSupport.TerminateAsync(state.Pid, TimeSpan.FromSeconds(10), ct);
        DaemonState.Clear(home);
        Console.WriteLine("Stopped.");
        return 0;
    }
}
