using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Throne.Infrastructure.Git;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Returns the resolved workspace root and the most recent disk-usage snapshot.
/// </summary>
public sealed class GetWorkspaceSettingsEndpoint(WorkspaceSizeProbe probe, IOptions<WorkspaceOptions> options)
{
    public ActionResult<WorkspaceSettingsDto> Run()
    {
        var snapshot = probe.Read();
        var hostRoot = options.Value.HostRoot;
        return new OkObjectResult(new WorkspaceSettingsDto
        {
            Root = snapshot.Root,
            Host_root = string.IsNullOrWhiteSpace(hostRoot) ? null : hostRoot,
            Status = snapshot.IsCalculating ? WorkspaceStatus.Calculating : WorkspaceStatus.Ready,
            Total_size_bytes = snapshot.TotalSizeBytes,
        });
    }
}
