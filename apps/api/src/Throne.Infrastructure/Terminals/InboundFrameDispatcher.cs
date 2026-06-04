using System.Globalization;
using System.Text;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Maps a parsed <see cref="ClientFrame"/> to the matching tmux subcommand
/// (<c>send-keys -H</c> for input bytes, <c>resize-window</c> for geometry).
/// Lives in its own type so <see cref="TerminalInboundPump"/> keeps a flat shape.
/// </summary>
internal sealed class InboundFrameDispatcher(TmuxCli tmux)
{
    public async Task DispatchAsync(ClientFrame frame, string sessionName, CancellationToken ct)
    {
        if (frame.Kind == ClientFrameKind.Input)
        {
            await SendInputAsync(sessionName, frame.Data!, ct);
            return;
        }
        if (frame.Kind == ClientFrameKind.Resize)
        {
            await SendResizeAsync(sessionName, frame.Cols, frame.Rows, ct);
        }
    }

    private async Task SendInputAsync(string sessionName, string data, CancellationToken ct)
    {
        if (data.Length == 0)
        {
            return;
        }

        // tmux send-keys -H трактует КАЖДЫЙ аргумент как один key-код. Многобайтовый
        // UTF-8 (кириллица и т.п.) нужно слать побайтово — один аргумент на байт.
        // Склейка в "D0B4" читается tmux как код 0xD0B4 и молча отбрасывается,
        // из-за чего проходил только однобайтовый ASCII.
        var bytes = Encoding.UTF8.GetBytes(data);
        var args = new List<string>(capacity: 4 + bytes.Length)
        {
            "send-keys", "-t", sessionName, "-H",
        };
        foreach (var b in bytes)
        {
            args.Add(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        await tmux.RunAsync(args, ct);
    }

    private async Task SendResizeAsync(string sessionName, int cols, int rows, CancellationToken ct) =>
        await tmux.RunAsync(
            [
                "resize-window", "-t", sessionName,
                "-x", cols.ToString(CultureInfo.InvariantCulture),
                "-y", rows.ToString(CultureInfo.InvariantCulture),
            ],
            ct);
}
