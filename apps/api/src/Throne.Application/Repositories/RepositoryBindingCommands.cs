namespace Throne.Application.Repositories;

/// <summary>
/// Bind a git repository to an intent (HTTP <c>POST /api/v1/intents/{intent_id}/repositories</c>,
/// see ADR-0024 § 1). MCP write-surface is intentionally absent in slice 1
/// (ADR-0024 § 8) — only the HTTP module dispatches this command.
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
/// disk cleanup is parked for slice 6 (ADR-0024 § 1, Out of scope).
/// </summary>
public sealed record UnbindRepositoryCommand(string IntentId, string BindingId);

/// <summary>
/// Manual PR-comment refresh (Q5 of the parent intent, ADR-0024 § 6). Synchronous on
/// purpose — the response carries the freshly observed comments back to the UI
/// kept in the same request. Background polling (T-10) still pushes per-comment
/// fanout for other open clients.
/// </summary>
public sealed record SyncRepositoryPullRequestCommand(string IntentId, string BindingId);
