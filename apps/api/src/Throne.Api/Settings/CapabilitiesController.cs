using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Terminals.Capabilities;
using Throne.Capabilities.Contracts.Generated;

namespace Throne.Api.Settings;

public sealed class CapabilitiesController(CapabilitiesService service) : CapabilitiesControllerBase
{
    public override async Task<ActionResult<ICollection<CapabilityDto>>> ListCapabilities()
    {
        var views = await service.ListAsync(HttpContext.RequestAborted);
        var dtos = views.Select(CapabilityDtoMapper.ToDto).ToArray();
        return Ok(dtos);
    }

    public override async Task<ActionResult<CapabilityDto>> SetCapabilitySelectedProvider(
        CapabilityName name,
        SetCapabilityProviderRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var view = await service.SetSelectedProviderAsync(
            CapabilityDtoMapper.ToDomainName(name),
            body.Selected_provider,
            HttpContext.RequestAborted
        );
        return Ok(CapabilityDtoMapper.ToDto(view));
    }
}
