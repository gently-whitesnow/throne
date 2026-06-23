namespace Throne.Application.Repositories;

/// <summary>
/// Bind a git repository to an intent (ADR-0024 § 1). Only the HTTP module
/// dispatches this command.
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
/// Restore the local clone of a binding whose workspace folder is missing (ADR-0024,
/// «Обновить» disk-recovery). Folder absent → re-queue the clone; folder present → no-op.
/// </summary>
public sealed record RefreshRepositoryBindingCommand(string IntentId, string BindingId);

/// <summary>
/// «Синхронизировать ветку»: hard-sync the local clone's current branch to its remote tip
/// (<c>git fetch</c> + <c>git reset --hard origin/{branch}</c>). Separate from «Обновить» —
/// this is the only action that touches the working tree, and the reset discards
/// uncommitted local changes (the UI confirm warns about it). Requires a ready clone whose
/// folder is present on disk.
/// </summary>
public sealed record SyncRepositoryBranchCommand(string IntentId, string BindingId);

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
