using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Vscode;

/// <summary>
/// Shell-out to the host <c>code</c> CLI to open VS Code on either the intent's aggregate
/// workspace directory (<c>{workspace_root}/intents/{intent_id}/</c>) or a single
/// repository binding's workspace. Capability-gated by <see cref="VscodeCapabilityGuard"/>.
///
/// The endpoint intentionally has no UI fallback inside docker-compose (Slice 2 Q4,
/// ADR-0026 § 7): the API container has no desktop CLI, so the front-end already hides
/// the buttons and we keep the surface honest by returning 422 here.
/// </summary>
public sealed class OpenInVscodeService(
    VscodeCapabilityGuard guard,
    VscodeSpawner spawner,
    IIntentRepository intents,
    RepositoryBindingResolver bindingResolver,
    IWorkspaceRootProvider workspaceRoot)
{
    public async Task<string> OpenIntentWorkspaceAsync(string intentId, CancellationToken ct)
    {
        await guard.EnsureReadyAsync(ct);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        var intent = await intents.GetByIdAsync(new IntentId(intentId), ct)
            ?? throw RepositoryBindingFailures.IntentNotFound(intentId);

        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intent.Id.Value);
        await spawner.SpawnAsync(workspacePath, ct);
        return workspacePath;
    }

    public async Task<string> OpenBindingWorkspaceAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        await guard.EnsureReadyAsync(ct);

        var binding = await bindingResolver.LoadBindingAsync(intentId, bindingId, ct);
        if (!string.Equals(binding.State.CloneStatus, CloneStatusNames.Ready, StringComparison.Ordinal))
        {
            throw VscodeFailures.BindingCloneNotReady(bindingId, binding.State.CloneStatus);
        }

        // Derive against the live ResolvedRoot, not binding.WorkspacePath: the persisted
        // path embeds the root that was in effect at clone-time and goes stale on a runtime
        // model switch (container /workspaces → native host ~/.throne/workspaces, ADR-0027).
        var workspacePath = WorkspacePathLayout.Compute(
            workspaceRoot.ResolvedRoot, binding.IntentId, binding.Coordinate);
        await spawner.SpawnAsync(workspacePath, ct);
        return workspacePath;
    }
}
