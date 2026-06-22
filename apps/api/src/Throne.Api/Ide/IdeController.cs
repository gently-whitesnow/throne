using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Ide;
using Throne.Ide.Contracts.Generated;

namespace Throne.Api.Ide;

/// <summary>
/// HTTP surface for the «open in IDE» carrier capability. Both endpoints are
/// fire-and-forget from the operator's perspective — once the spawn completes
/// they return <c>202 Accepted</c> with the resolved provider and absolute path.
/// </summary>
public sealed class IdeController(OpenInIdeService service) : IdeControllerBase
{
    public override async Task<ActionResult<OpenInIdeResponse>> OpenIntentInIde(string intent_id)
    {
        var result = await service.OpenIntentAsync(intent_id, HttpContext.RequestAborted);
        return Accepted(ToDto(result));
    }

    public override async Task<ActionResult<OpenInIdeResponse>> OpenBindingInIde(
        string intent_id,
        string binding_id)
    {
        var result = await service.OpenBindingAsync(intent_id, binding_id, HttpContext.RequestAborted);
        return Accepted(ToDto(result));
    }

    private static OpenInIdeResponse ToDto(OpenInIdeResult result) => new()
    {
        Workspace_path = result.WorkspacePath,
        Provider = result.ProviderName,
    };
}
