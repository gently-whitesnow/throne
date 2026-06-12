using System.Text;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Guards the spawn argv against tmux's command-size ceiling. The tmux client packs the whole
/// <c>new-session</c> command (argc + every argument) into a single imsg whose payload is bounded by
/// <c>MAX_IMSGSIZE</c> (16384 bytes); past that the client aborts with a bare <c>command too long</c>
/// and exit 1. With the rules block now file-backed the only argv token that can still grow is the
/// positional task, so this turns that residual overflow into an actionable error instead of tmux's
/// opaque one. The budget stays conservatively below 16384 to leave room for imsg + per-arg framing.
/// </summary>
internal static class TmuxCommandLimit
{
    private const int MaxArgvBytes = 15000;

    public static bool Exceeds(IReadOnlyList<string> args, out string detail)
    {
        var bytes = 0;
        foreach (var arg in args)
        {
            bytes += Encoding.UTF8.GetByteCount(arg) + 1; // +1 ≈ the NUL tmux frames each arg with
        }

        if (bytes > MaxArgvBytes)
        {
            detail =
                $"spawn command is {bytes} bytes — over tmux's ~{MaxArgvBytes}-byte limit; "
                + "trim the task text (the rules block is already file-backed).";
            return true;
        }

        detail = string.Empty;
        return false;
    }
}
