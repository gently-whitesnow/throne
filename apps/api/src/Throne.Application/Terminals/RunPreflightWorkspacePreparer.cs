using Throne.Application.Git;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// One-shot workspace preparation run at the very start of every spawn, before the agent boots: trust
/// the workspace for the vendor, wipe Throne-managed per-run staging from any prior run
/// (<see cref="WorkspaceStagingReset"/>), then dump the intent's attachments
/// (<see cref="WorkspaceAttachmentDumper"/>). Folded into a single collaborator so
/// <see cref="RunPreflightSpawn"/> stays within the constructor-dependency budget and the ordering
/// (trust → reset → dump, before the adapter re-seeds skills) lives in one place.
/// </summary>
public sealed class RunPreflightWorkspacePreparer(
    IWorkspaceTrust workspaceTrust,
    ISessionSkillCatalog skillCatalog,
    WorkspaceAttachmentDumper attachmentDumper)
{
    public async Task PrepareAsync(IntentId intentId, string vendor, string workspacePath, CancellationToken ct)
    {
        // Trust the workspace before the agent boots in it, otherwise the CLI blocks on its interactive
        // trust prompt and the operator has to confirm by hand on every run. Which trust store gets
        // seeded depends on the launched vendor.
        await workspaceTrust.EnsureTrustedAsync(vendor, workspacePath, ct);

        // Reset before re-seeding (skills via the adapter, attachments below) so nothing from a prior
        // run — a skill tree written in a different mode, an upstream-deleted attachment — leaks in.
        WorkspaceStagingReset.Reset(workspacePath, skillCatalog.List().Select(skill => skill.Id));
        await attachmentDumper.DumpAsync(intentId, workspacePath, ct);
    }
}
