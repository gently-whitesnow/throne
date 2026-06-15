using System.Text;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Guards the spawn argv against tmux's command-size ceiling. The tmux client packs the whole
/// <c>new-session</c> command (argc + every argument) into a single imsg whose payload is bounded by
/// <c>MAX_IMSGSIZE</c> (16384 bytes); past that the client aborts with a bare <c>command too long</c>
/// and exit 1. Both the rules block and the user task are file-backed now (rules via the vendor's
/// system-prompt-file flag / profile, task via post-spawn <c>load-buffer</c> + <c>paste-buffer</c>),
/// so the spawn argv only carries small reference tokens. This guard stays as a backstop for future
/// argv additions — if it ever fires, the new token also needs to move off the argv. The budget
/// stays conservatively below 16384 to leave room for imsg + per-arg framing.
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
                + "rules and task are already file-backed, so a new argv token must have grown — move it off the argv.";
            return true;
        }

        detail = string.Empty;
        return false;
    }
}
