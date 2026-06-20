namespace Throne.Infrastructure.Terminals;

internal interface IOpencodeTuiClient
{
    /// <summary>
    /// Creates a session on the shared serve endpoint and submits the initial prompt pinned to the
    /// given provider/model, returning the created <c>sessionID</c>. The id is what the operator's
    /// <c>opencode attach … --session &lt;id&gt;</c> front uses to pull the running session — there
    /// is no TUI command-bus push, so nothing to race.
    /// </summary>
    Task<string> CreateSessionAndSubmitAsync(
        Uri endpoint,
        string workspacePath,
        string providerId,
        string modelId,
        string prompt,
        CancellationToken ct);
}
