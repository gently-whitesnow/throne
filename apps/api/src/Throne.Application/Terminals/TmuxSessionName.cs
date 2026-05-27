namespace Throne.Application.Terminals;

/// <summary>
/// Deterministic tmux session name per intent (`throne-{intent_id}`). Slice 2 ADR-0026 § 2
/// fixes this format so that <c>tmux has-session</c> is the single source of truth for
/// session liveness — Throne persists nothing about the session.
/// </summary>
public static class TmuxSessionName
{
    public const string Prefix = "throne-";

    public static string For(string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        return Prefix + intentId;
    }
}
