namespace Throne.Application.Repositories;

/// <summary>
/// Bind a git repository to an intent (ADR-0024 § 1). MCP write-surface is
/// intentionally absent (ADR-0024 § 8) — only the HTTP module dispatches this command.
/// </summary>
public sealed record BindRepositoryCommand(
    string IntentId,
    string Provider,
    string Owner,
    string Repo,
    string? DefaultBranch,
    int? PullRequestNumber);

/// <summary>
/// Unbind a previously bound repository. Workspace directory is NOT removed —
/// disk cleanup is out of scope (ADR-0024 § 1).
/// </summary>
public sealed record UnbindRepositoryCommand(string IntentId, string BindingId);

/// <summary>
/// Manual PR-comment refresh (ADR-0024 § 6). Synchronous on purpose — the response
/// carries the freshly observed comments back to the UI in the same request.
/// Background polling still pushes per-comment fanout for other open clients.
/// </summary>
public sealed record SyncRepositoryPullRequestCommand(string IntentId, string BindingId);
