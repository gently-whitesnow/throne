using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Vscode;
using Throne.Vscode.Contracts.Generated;

namespace Throne.Api.Vscode;

public sealed class VscodeController(OpenInVscodeService service) : VscodeControllerBase
{
    public override async Task<ActionResult<OpenInVscodeResponse>> OpenIntentInVscode(string intent_id)
    {
        try
        {
            var workspacePath = await service.OpenIntentWorkspaceAsync(intent_id, HttpContext.RequestAborted);
            return Accepted(new OpenInVscodeResponse { Workspace_path = workspacePath });
        }
        catch (ApiException ex)
        {
            return Map(ex);
        }
    }

    public override async Task<ActionResult<OpenInVscodeResponse>> OpenBindingInVscode(
        string intent_id,
        string binding_id)
    {
        try
        {
            var workspacePath = await service.OpenBindingWorkspaceAsync(intent_id, binding_id, HttpContext.RequestAborted);
            return Accepted(new OpenInVscodeResponse { Workspace_path = workspacePath });
        }
        catch (ApiException ex)
        {
            return Map(ex);
        }
    }

    private static ActionResult<OpenInVscodeResponse> Map(ApiException ex) => ex.Code switch
    {
        ErrorCodes.IntentNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
        ErrorCodes.RepositoryBindingNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("Binding not found", ex.Detail)),
        ErrorCodes.CapabilityDisabled
            or ErrorCodes.TerminalSpawnFailed
            or ErrorCodes.RepositoryNotReady =>
            new UnprocessableEntityObjectResult(
                ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Open in VS Code rejected", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
    };
}
