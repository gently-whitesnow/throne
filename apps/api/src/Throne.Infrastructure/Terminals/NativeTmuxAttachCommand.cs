namespace Throne.Infrastructure.Terminals;

internal static class NativeTmuxAttachCommand
{
    public static string BuildShellCommand(string sessionName)
    {
        var session = TerminalCommandEscaping.ShellSingleQuote(sessionName);
        return string.Join(
            " ",
            $"tmux set-window-option -t {session} window-size latest;",
            $"tmux set-option -t {session} mouse on;",
            $"( for _ in 1 2 3 4 5 6 7 8 9 10; do",
            $"tmux list-clients -t {session} >/dev/null 2>&1 && break;",
            $"sleep 0.1; done;",
            $"tmux resize-window -A -t {session} >/dev/null 2>&1;",
            $"tmux set-window-option -t {session} window-size latest >/dev/null 2>&1 ) &",
            $"exec tmux attach -d -t {session}");
    }
}
