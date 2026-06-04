using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Persistence-orchestration helper. Owns construction of fresh bindings
/// (workspace-path layout, factory invocation, clock) and the create/delete/save/find
/// roundtrips through the unit-of-work + Mongo port. Delete also removes the binding's
/// on-disk workspace directory — the folder is part of the binding's lifecycle.
/// </summary>
public sealed class RepositoryBindingPersistence(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IWorkspaceRootProvider workspace,
    IWorkspaceDirectoryRemover workspaceRemover)
{
    public IntentRepositoryBinding BuildPendingBinding(
        BindRepositoryCommand command,
        IntentId intentId)
    {
        ArgumentNullException.ThrowIfNull(command);
        RepoCoordinate coordinate;
        try
        {
            coordinate = new RepoCoordinate(command.Provider, command.Owner, command.Repo);
        }
        catch (ArgumentException ex)
        {
            // Surface owner/repo allow-list / length / `..` traversal violations as a
            // 422 validation failure instead of letting the raw ArgumentException
            // bubble to ASP.NET's default 500 handler. ADR-0024 § 1 invariant.
            throw RepositoryBindingFailures.InvalidCoordinate(command.Owner, command.Repo, ex.Message);
        }
        var defaultBranch = string.IsNullOrWhiteSpace(command.DefaultBranch) ? "main" : command.DefaultBranch.Trim();
        var workspacePath = WorkspacePathLayout.Compute(workspace.ResolvedRoot, intentId, coordinate);
        return IntentRepositoryBinding.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: coordinate,
            defaultBranch: defaultBranch,
            workspacePath: workspacePath,
            pullRequestNumber: command.PullRequestNumber,
            now: clock.GetUtcNow());
    }

    public async Task<IntentRepositoryBinding> CreateAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        var outcome = await unitOfWork.ExecuteAsync(inner => bindings.CreateAsync(binding, inner), ct);
        return outcome switch
        {
            CreateBindingOutcome.Created c => c.Binding,
            CreateBindingOutcome.Duplicate dup => throw RepositoryBindingFailures.DuplicateBinding(dup.Existing),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }

    /// <summary>
    /// Remove the on-disk workspace directory first, then the record. A failed
    /// directory delete keeps the record so the user can retry instead of being
    /// left with an orphaned folder and no row to act on.
    /// </summary>
    public async Task DeleteAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        // Recompute against the live root, not binding.WorkspacePath: the persisted path
        // embeds the root in effect at clone-time and goes stale on a runtime model
        // switch (container ↔ native host, ADR-0027) — same reasoning as OpenInVscode.
        var workspacePath = WorkspacePathLayout.Compute(
            workspace.ResolvedRoot, binding.IntentId, binding.Coordinate);
        try
        {
            await workspaceRemover.RemoveAsync(workspacePath, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ApiException(
                ErrorCodes.RepositoryWorkspaceRemovalFailed,
                $"Не удалось удалить папку репозитория на диске ('{workspacePath}'): {ex.Message}. "
                    + "Репозиторий не удалён — закройте процессы, держащие папку, и повторите.",
                new Dictionary<string, object?>
                {
                    ["binding_id"] = binding.Id.Value,
                    ["workspace_path"] = workspacePath,
                });
        }

        var outcome = await unitOfWork.ExecuteAsync(inner => bindings.DeleteAsync(binding.Id, inner), ct);
        if (outcome is DeleteBindingOutcome.NotFound)
        {
            throw RepositoryBindingFailures.BindingNotFound(binding.IntentId.Value, binding.Id.Value);
        }
    }

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(IntentId intentId, CancellationToken ct) =>
        bindings.FindByIntentAsync(intentId, ct);
}
