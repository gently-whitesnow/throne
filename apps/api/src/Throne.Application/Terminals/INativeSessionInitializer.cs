namespace Throne.Application.Terminals;

/// <summary>
/// A session-hook adapter that owns its own spawn: it boots/finds the agent server, creates the
/// session and submits the initial prompt <em>before</em> the operator-visible tmux pane starts,
/// then hands back the argv that pane must run to attach to the already-running session.
///
/// Used by OpenCode, whose agent loop lives in a shared persistent <c>opencode serve</c> while the
/// tmux pane runs a thin <c>opencode attach &lt;url&gt; --session &lt;id&gt;</c> front. Because the
/// loop must be live and the session id known before the front can be told what to display, prompt
/// delivery cannot wait until after spawn (the old post-spawn <c>select-session</c> push raced the
/// front's command-bus subscription). The front pulling the session by id removes that race.
/// </summary>
public interface INativeSessionInitializer
{
    /// <param name="model">
    /// Resolved model id for this launch (the adapter prefixes it with its provider id when it
    /// pins the model on the server-side prompt). Pinning it here is what makes the loop run on the
    /// chosen model — the attach front carries no model flag.
    /// </param>
    /// <param name="userPrompt">
    /// Initial task. Null/blank boots the front bare against the workspace with no session pinned,
    /// so the operator types the first prompt themselves.
    /// </param>
    /// <returns>argv appended after the vendor command to spawn the attach front.</returns>
    Task<IReadOnlyList<string>> InitializeSessionAsync(
        string intentId,
        string workspacePath,
        string model,
        string? userPrompt,
        CancellationToken ct);
}
