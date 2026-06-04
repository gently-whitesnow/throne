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

    /// <summary>
    /// Inverse of <see cref="For"/>: pulls the intent id out of a <c>throne-{intent_id}</c>
    /// session name. Returns <c>null</c> for any name that is not a throne session or carries
    /// an empty id, so callers can filter <c>tmux ls</c> output down to real intent ids.
    /// </summary>
    public static string? TryIntentId(string sessionName)
    {
        if (string.IsNullOrEmpty(sessionName) || !sessionName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }
        var id = sessionName[Prefix.Length..];
        return id.Length == 0 ? null : id;
    }
}
