using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Bulk workspace cleanup: a mass unbind that frees disk under <c>Throne:Workspace:Root</c>.
/// Reuses <see cref="RepositoryBindingPersistence.DeleteAsync"/> per binding so each removal
/// stays consistent (clone folder + binding record + <c>IntentRepositoryUnbound</c> event)
/// and never leaves an orphaned (folder-less) record. Touches only per-intent things —
/// the <see cref="Repository"/> registry and its artifacts live in Mongo, off-disk, and are
/// never cascaded.
/// </summary>
public sealed class WorkspaceCleanupService(
    IIntentRepositoryBindingRepository bindings,
    IIntentRepository intents,
    RepositoryBindingPersistence persistence,
    IWorkspaceRootProvider workspace,
    IWorkspaceDirectorySizer sizer,
    IWorkspaceDirectoryRemover remover)
{
    public Task<WorkspaceCleanResult> CleanAsync(WorkspaceCleanMode mode, bool dryRun, CancellationToken ct) =>
        mode switch
        {
            WorkspaceCleanMode.All => CleanAllAsync(dryRun, ct),
            WorkspaceCleanMode.ClosedOnly => CleanClosedAsync(dryRun, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown workspace clean mode."),
        };

    private async Task<WorkspaceCleanResult> CleanAllAsync(bool dryRun, CancellationToken ct)
    {
        var root = workspace.ResolvedRoot;
        var all = await bindings.FindAllAsync(ct);
        var sizeBefore = sizer.Measure(root);

        if (dryRun)
        {
            return new WorkspaceCleanResult(all.Count, sizeBefore, DryRun: true);
        }

        foreach (var binding in all)
        {
            await persistence.DeleteAsync(binding, ct);
        }

        // Clear leftover empty intent folders + any orphan clones (folders without a binding)
        // so the root ends up empty, matching the operator's "remove everything" intent.
        await remover.RemoveContentsAsync(root, ct);

        var freed = Math.Max(0, sizeBefore - sizer.Measure(root));
        return new WorkspaceCleanResult(all.Count, freed, DryRun: false);
    }

    private async Task<WorkspaceCleanResult> CleanClosedAsync(bool dryRun, CancellationToken ct)
    {
        var root = workspace.ResolvedRoot;
        var closedIntents = await intents.ListAsync(IntentStatusNames.Terminal, ct);

        var targets = new List<IntentRepositoryBinding>();
        foreach (var intent in closedIntents)
        {
            targets.AddRange(await bindings.FindByIntentAsync(intent.Id, ct));
        }

        long freed = 0;
        foreach (var binding in targets)
        {
            var path = WorkspacePathLayout.Compute(root, binding.IntentId, binding.Coordinate);
            freed += sizer.Measure(path);
        }

        if (dryRun)
        {
            return new WorkspaceCleanResult(targets.Count, freed, DryRun: true);
        }

        foreach (var binding in targets)
        {
            await persistence.DeleteAsync(binding, ct);
        }

        return new WorkspaceCleanResult(targets.Count, freed, DryRun: false);
    }
}

public enum WorkspaceCleanMode
{
    All,
    ClosedOnly,
}

public sealed record WorkspaceCleanResult(int RemovedClones, long FreedBytes, bool DryRun);
