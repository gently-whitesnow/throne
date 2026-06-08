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
    int? PullRequestNumber,
    string? Host = null,
    int? ProjectId = null);

/// <summary>
/// Delete a previously bound repository: removes the binding record AND its on-disk
/// workspace directory (ADR-0024 § 1, revised).
/// </summary>
public sealed record UnbindRepositoryCommand(string IntentId, string BindingId);

/// <summary>
/// Manual PR-comment refresh (ADR-0024 § 6). Synchronous on purpose — the response
/// carries the freshly observed comments back to the UI in the same request.
/// Background polling still pushes per-comment fanout for other open clients.
/// </summary>
public sealed record SyncRepositoryPullRequestCommand(string IntentId, string BindingId);

/// <summary>
/// Attach a pull request to an already-bound repository without delete/rebind (intent spec C).
/// Fills the empty PR slot of an existing binding; the aggregate rejects a second attach.
/// This is the manual counterpart of the auto-bind pass and the only supported way to point a
/// secondary intent's binding at a shared PR.
/// </summary>
public sealed record AttachRepositoryPullRequestCommand(string IntentId, string BindingId, int PullRequestNumber);
