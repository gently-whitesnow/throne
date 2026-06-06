using Microsoft.AspNetCore.Mvc;
using Throne.Application.Repositories;
using Throne.Settings.Contracts.Generated;
using DomainCleanMode = Throne.Application.Repositories.WorkspaceCleanMode;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Bulk-removes intent repository clones under the workspace root. Delegates the
/// consistent per-binding delete (folder + record + event) to
/// <see cref="WorkspaceCleanupService"/>, then drops the size cache so the card
/// recomputes. With <c>dry_run=true</c> nothing is deleted — the summary is the
/// confirm-dialog preview.
/// </summary>
public sealed class CleanWorkspaceEndpoint(WorkspaceCleanupService cleanup, WorkspaceSizeProbe probe)
{
    public async Task<ActionResult<WorkspaceCleanResultDto>> RunAsync(
        WorkspaceCleanRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mode = Map(request.Mode);
        var result = await cleanup.CleanAsync(mode, request.Dry_run, ct);

        if (!result.DryRun)
        {
            probe.Invalidate();
        }

        return new OkObjectResult(new WorkspaceCleanResultDto
        {
            Removed_clones = result.RemovedClones,
            Freed_bytes = result.FreedBytes,
            Dry_run = result.DryRun,
        });
    }

    private static DomainCleanMode Map(Throne.Settings.Contracts.Generated.WorkspaceCleanMode mode) => mode switch
    {
        Throne.Settings.Contracts.Generated.WorkspaceCleanMode.All => DomainCleanMode.All,
        Throne.Settings.Contracts.Generated.WorkspaceCleanMode.Closed_only => DomainCleanMode.ClosedOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown workspace clean mode."),
    };
}
