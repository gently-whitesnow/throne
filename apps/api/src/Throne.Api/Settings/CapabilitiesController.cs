using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Errors;
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

    public override async Task<ActionResult<CapabilityDto>> SetCapabilityEnabled(
        CapabilityName name,
        SetCapabilityEnabledRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var view = await service.ToggleAsync(
                CapabilityDtoMapper.ToDomainName(name),
                body.Enabled,
                HttpContext.RequestAborted);
            return Ok(CapabilityDtoMapper.ToDto(view));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.CapabilityNotFound)
        {
            return NotFound(ApiProblems.NotFound("Capability not found", ex.Detail));
        }
    }
}
