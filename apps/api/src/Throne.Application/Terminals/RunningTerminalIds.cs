namespace Throne.Application.Terminals;

/// <summary>
/// Resolves the live set of intent ids that currently own a tmux terminal session.
/// tmux is the single source of truth (ADR-0026 § 2) — every call re-reads
/// <c>tmux list-sessions</c>, so the result can never drift from reality. Shared by the
/// context counter and the <c>terminal_running</c> list filter.
/// </summary>
public static class RunningTerminalIds
{
    public static async Task<IReadOnlyList<string>> ResolveAsync(ITmuxSessionManager tmux, CancellationToken ct)
    {
        var sessions = await tmux.ListThroneSessionsAsync(ct);
        var ids = new List<string>(sessions.Count);
        foreach (var name in sessions)
        {
            var id = TmuxSessionName.TryIntentId(name);
            if (id is not null)
            {
                ids.Add(id);
            }
        }
        return ids;
    }
}
