using Microsoft.AspNetCore.Mvc;
using Throne.Application.LocalModels;
using Throne.Settings.Contracts.Generated;
using DiscoveryStatus = Throne.Application.LocalModels.LocalModelDiscoveryStatus;
using WireStatus = Throne.Settings.Contracts.Generated.LocalModelDiscoveryStatus;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Projects a local-model discovery probe into the wire DTO for
/// <c>GET /api/v1/settings/local-model/models</c>. Every discovery state maps to a 200 — the
/// controlled fallback lives in the payload, not the HTTP status, so the settings UI renders a
/// state instead of an error boundary.
/// </summary>
public sealed class GetLocalModelCatalogEndpoint(LocalModelDiscoveryService discovery)
{
    public async Task<ActionResult<LocalModelCatalogDto>> RunAsync(CancellationToken ct)
    {
        var result = await discovery.DiscoverAsync(ct);
        var dto = new LocalModelCatalogDto
        {
            Status = ToWireStatus(result.Status),
            Base_url = result.BaseUrl!,
            Models = result.Models.ToList(),
            Error = result.Error!,
        };
        return new OkObjectResult(dto);
    }

    private static WireStatus ToWireStatus(DiscoveryStatus status) => status switch
    {
        DiscoveryStatus.NotConfigured => WireStatus.Not_configured,
        DiscoveryStatus.Ready => WireStatus.Ready,
        DiscoveryStatus.Empty => WireStatus.Empty,
        DiscoveryStatus.Unreachable => WireStatus.Unreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(status), $"Unknown discovery status '{status}'."),
    };
}
