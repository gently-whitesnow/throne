using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Vscode;
using Throne.Vscode.Contracts.Generated;

namespace Throne.Api.Vscode;

public sealed class VscodeController(OpenInVscodeService service) : VscodeControllerBase
{
    public override async Task<ActionResult<OpenInVscodeResponse>> OpenIntentInVscode(
        string intent_id
    )
    {
        var workspacePath = await service.OpenIntentWorkspaceAsync(
            intent_id,
            HttpContext.RequestAborted
        );
        return Accepted(new OpenInVscodeResponse { Workspace_path = workspacePath });
    }

    public override async Task<ActionResult<OpenInVscodeResponse>> OpenBindingInVscode(
        string intent_id,
        string binding_id
    )
    {
        var workspacePath = await service.OpenBindingWorkspaceAsync(
            intent_id,
            binding_id,
            HttpContext.RequestAborted
        );
        return Accepted(new OpenInVscodeResponse { Workspace_path = workspacePath });
    }
}
