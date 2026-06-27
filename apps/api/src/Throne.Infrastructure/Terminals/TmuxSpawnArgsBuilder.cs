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
        var env = request.EnvironmentVariables ?? new Dictionary<string, string>();
        var args = new List<string>(capacity: 12 + (env.Count * 2) + request.Arguments.Count)
        {
            "new-session",
            "-A",
            "-D",
            "-s", sessionName,
            "-c", request.WorkingDirectory,
            "-d",
        };

        // Spawn at the client geometry when known: the agent renders at the final size from its
        // first paint, so the client's initial resize is a no-op and the TUI never reflows into a
        // duplicated scrollback frame. Bounds mirror the WebSocket resize validator.
        if (IsValidDimension(request.Cols) && IsValidDimension(request.Rows))
        {
            args.Add("-x");
            args.Add(request.Cols!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            args.Add("-y");
            args.Add(request.Rows!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach (var pair in env.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                args.Add("-e");
                args.Add($"{pair.Key}={pair.Value}");
            }
        }
        args.Add(request.Command);
        args.AddRange(request.Arguments);
        return args;
    }

    private static bool IsValidDimension(int? value) =>
        value is >= TerminalFrames.MinDimension and <= TerminalFrames.MaxDimension;

    private static void Validate(TmuxSpawnRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IntentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Command);
    }
}
