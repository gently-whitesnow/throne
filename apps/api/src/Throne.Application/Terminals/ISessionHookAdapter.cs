namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral per-session spawn-arg injection for the embedded terminal (ADR-0034). One
/// implementation per agent CLI; <see cref="RunPreflightSpawn"/> selects it by <see cref="Vendor"/>
/// and appends the returned tokens to the spawn argv (before the positional prompt).
///
/// Two vendor-specific concerns live behind this seam, both expressed as small reference tokens so
/// no bulky payload reaches the ~16 KB tmux spawn argv:
/// <list type="bullet">
///   <item>The shared hook <em>vector</em> — agent runtime events (<c>Stop</c> +
///   <c>UserPromptSubmit</c>) bound to the local
///   <c>POST /api/v1/intents/{id}/terminal/hooks/{event}</c> callback. Claude takes a settings file,
///   Codex takes inline <c>-c</c> overrides, and OpenCode loads a project-local plugin.</item>
///   <item>The assembled rules block (ADR-0034 upfront context). It is multi-KB, so it is written
///   to a per-session file and referenced by a small flag — Claude
///   <c>--append-system-prompt-file</c>, Codex a generated <c>-p</c> profile carrying
///   <c>developer_instructions</c>. Inlining it as an argv token overflowed tmux's
///   <c>MAX_IMSGSIZE</c> (<c>command too long</c>).</item>
/// </list>
/// </summary>
public interface ISessionHookAdapter
{
    string Vendor { get; }

    string? ReadinessHookEvent => null;

    /// <summary>
    /// Prepares the per-session spawn args for <paramref name="intentId"/> launched in
    /// <paramref name="workspacePath"/> with spawn <paramref name="mode"/> and returns the extra CLI
    /// tokens to append to the spawn argv. The mode is baked into the hook callback URL so the
    /// endpoint can return the session to its spawn phase on <c>UserPromptSubmit</c> statelessly.
    /// <paramref name="systemPrompt"/> is the assembled rules block — null/blank injects no system
    /// context. When present it is written to a per-session file (never inlined on the argv) and the
    /// returned tokens reference that file. May write per-session files under
    /// <paramref name="workspacePath"/> or the vendor's config home as a side effect.
    /// </summary>
    Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId,
        string workspacePath,
        string mode,
        string? systemPrompt,
        ReviewArtifactWriteTarget? reviewArtifact,
        CancellationToken ct);

    /// <summary>
    /// Removes any per-session files this adapter wrote OUTSIDE the intent workspace, called on
    /// intent-done teardown. Workspace-local files (Claude's settings + system-prompt) are reaped by
    /// the workspace-folder removal and need nothing here; Codex's <c>$CODEX_HOME</c> profile lives
    /// outside the workspace and is deleted here. Best-effort — the caller swallows failures.
    /// </summary>
    Task CleanupAsync(string intentId, CancellationToken ct);

    /// <summary>
    /// Legacy vendor-specific readiness predicate over a <c>tmux capture-pane</c> snapshot.
    /// Kept until every vendor has a provider-native <see cref="ReadinessHookEvent"/>.
    /// </summary>
    bool IsTuiReady(string paneSnapshot);
}
