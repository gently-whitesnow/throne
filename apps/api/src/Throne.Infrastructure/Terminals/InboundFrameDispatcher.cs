using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Maps a parsed <see cref="ClientFrame"/> to the matching tmux subcommand
/// (<c>send-keys -H</c> for input bytes, <c>resize-window</c> for geometry).
/// Lives in its own type so <see cref="TerminalInboundPump"/> keeps a flat shape.
/// </summary>
internal sealed class InboundFrameDispatcher(TmuxCli tmux, ILogger? log = null)
{
    private readonly ILogger _log = log ?? NullLogger.Instance;

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
        var controlHex = ControlHex(bytes);
        if (controlHex == NoControlBytes)
        {
            TerminalsLog.TerminalInputFrame(_log, sessionName, data.Length, bytes.Length, controlHex);
        }
        else
        {
            TerminalsLog.TerminalControlInputFrame(_log, sessionName, data.Length, bytes.Length, controlHex);
        }

        // send-keys -H spends ~3 argv bytes per input byte; tmux packs the whole command into a
        // single imsg capped at MAX_IMSGSIZE (16384), so a large paste overflows it and the frame
        // is dropped whole (see TmuxCommandLimit). Past the threshold route through a tmux buffer,
        // whose argv stays tiny regardless of payload size.
        if (bytes.Length > MaxInlineInputBytes)
        {
            await SendViaBufferAsync(sessionName, data, bytes.Length, controlHex, ct);
            return;
        }

        var args = new List<string>(capacity: 4 + bytes.Length)
        {
            "send-keys", "-t", sessionName, "-H",
        };
        foreach (var b in bytes)
        {
            args.Add(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        var outcome = await tmux.RunAsync(args, ct);
        if (!outcome.IsSuccess)
        {
            TerminalsLog.TerminalInputSendFailed(
                _log,
                sessionName,
                data.Length,
                bytes.Length,
                controlHex,
                TmuxOutcomeDetail.Extract(outcome) ?? "<empty>");
        }
    }

    private async Task SendViaBufferAsync(
        string sessionName,
        string data,
        int byteLength,
        string controlHex,
        CancellationToken ct)
    {
        // load-buffer reads the payload from stdin (argv carries only the buffer name), then
        // paste-buffer injects it. -p emits bracketed-paste markers only if the pane's app asked
        // for them, so embedded newlines arrive as paste content (no premature submit in claude/
        // codex) while plain shells are unaffected. No trailing Enter — this is live input, not a
        // submit. -d drops the buffer afterwards; input is serialized per session, so the fixed
        // buffer name cannot collide with a concurrent paste.
        var bufferName = sessionName + "-input";
        var load = await tmux.RunAsync(["load-buffer", "-b", bufferName, "-"], ct, standardInput: data);
        var paste = load.IsSuccess
            ? await tmux.RunAsync(["paste-buffer", "-d", "-p", "-b", bufferName, "-t", sessionName], ct)
            : load;

        TerminalsLog.TerminalInputBuffered(_log, sessionName, data.Length, byteLength, bufferName, paste.IsSuccess);
        if (!paste.IsSuccess)
        {
            TerminalsLog.TerminalInputSendFailed(
                _log,
                sessionName,
                data.Length,
                byteLength,
                controlHex,
                TmuxOutcomeDetail.Extract(paste) ?? "<empty>");
        }
    }

    // Keeps the send-keys -H argv (~3 bytes/input byte plus framing) safely under tmux's
    // ~16384-byte imsg ceiling; larger frames take the buffer path instead.
    private const int MaxInlineInputBytes = 4096;

    private async Task SendResizeAsync(string sessionName, int cols, int rows, CancellationToken ct) =>
        await tmux.RunAsync(
            [
                "resize-window", "-t", sessionName,
                "-x", cols.ToString(CultureInfo.InvariantCulture),
                "-y", rows.ToString(CultureInfo.InvariantCulture),
            ],
            ct);

    private static string ControlHex(byte[] bytes)
    {
        var controlBytes = bytes
            .Where(b => b < 0x20 || b == 0x7F)
            .Take(16)
            .Select(b => b.ToString("X2", CultureInfo.InvariantCulture))
            .ToArray();

        if (controlBytes.Length == 0)
        {
            return NoControlBytes;
        }

        var suffix = bytes.Count(b => b < 0x20 || b == 0x7F) > controlBytes.Length
            ? ",..."
            : string.Empty;
        return string.Join(",", controlBytes) + suffix;
    }

    private const string NoControlBytes = "<none>";
}
