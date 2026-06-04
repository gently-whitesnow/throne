using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Builds the argument vector for <c>tmux new-session -ADs ...</c>. Pulled out of
/// <see cref="TmuxSessionManager"/> so the manager itself stays under the per-type
/// cyclomatic budget.
/// </summary>
internal static class TmuxSpawnArgsBuilder
{
    public static List<string> Build(string sessionName, TmuxSpawnRequest request)
    {
        Validate(request);

        // -A: attach if it already exists; -D: detach others; -s: name; -c: cwd; -d: stay detached.
        // After the command argv tmux runs `command args...` directly under its PTY.
        var args = new List<string>(capacity: 8 + request.Arguments.Count)
        {
            "new-session",
            "-A",
            "-D",
            "-s", sessionName,
            "-c", request.WorkingDirectory,
            "-d",
            request.Command,
        };
        args.AddRange(request.Arguments);
        return args;
    }

    private static void Validate(TmuxSpawnRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IntentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Command);
    }
}
