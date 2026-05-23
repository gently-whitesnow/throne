using Microsoft.AspNetCore.Mvc;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/settings/workspace</c> (D5 of T-11).
/// Returns the resolved workspace root and the most recent disk-usage snapshot.
/// </summary>
public sealed class GetWorkspaceSettingsEndpoint(WorkspaceSizeProbe probe)
{
    public ActionResult<WorkspaceSettingsDto> Run()
    {
        var snapshot = probe.Read();
        return new OkObjectResult(new WorkspaceSettingsDto
        {
            Root = snapshot.Root,
            Status = snapshot.IsCalculating ? WorkspaceStatus.Calculating : WorkspaceStatus.Ready,
            Total_size_bytes = snapshot.TotalSizeBytes ?? 0,
        });
    }
}
