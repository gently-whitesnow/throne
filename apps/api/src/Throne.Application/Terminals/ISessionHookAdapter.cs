namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral per-session hook injection for the embedded terminal (ADR-0034). One
/// implementation per agent CLI; <see cref="RunPreflightSpawn"/> selects it by <see cref="Vendor"/>
/// and appends the returned tokens to the spawn argv (before the positional prompt).
///
/// The hook <em>vector</em> is shared — agent runtime events (<c>Stop</c> + <c>UserPromptSubmit</c>)
/// bound to the local <c>POST /api/v1/intents/{id}/terminal/hooks/{event}</c> callback. How that
/// vector is expressed is vendor-specific and stays behind this adapter: Claude takes a settings
/// file via <c>--settings</c>, Codex takes an inline <c>-c</c> config override. Adding
/// <c>SessionStart</c> later extends the adapter, not the spawn path.
/// </summary>
public interface ISessionHookAdapter
{
    string Vendor { get; }

    /// <summary>
    /// Prepares the per-session hooks for <paramref name="intentId"/> launched in
    /// <paramref name="workspacePath"/> with spawn <paramref name="mode"/> and returns the extra CLI
    /// tokens to append to the spawn argv. The mode is baked into the hook callback URL so the
    /// endpoint can return the session to its spawn phase on <c>UserPromptSubmit</c> statelessly.
    /// May write a per-session file under <paramref name="workspacePath"/> as a side effect
    /// (Claude); returns the tokens only, with no file, for vendors that inject inline (Codex).
    /// </summary>
    Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId, string workspacePath, string mode, CancellationToken ct);
}
