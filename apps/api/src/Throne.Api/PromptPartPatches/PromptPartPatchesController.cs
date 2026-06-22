using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.PromptPartPatches;
using Throne.PromptPartPatches.Contracts.Generated;

namespace Throne.Api.PromptPartPatches;

public sealed class PromptPartPatchesController(
    ListPromptPartPatchesHandler listHandler,
    GetPromptPartPatchHandler getHandler,
    ProposePromptPartPatchHandler proposeHandler,
    ApplyPromptPartPatchHandler applyHandler,
    RejectPromptPartPatchHandler rejectHandler
) : PromptPartPatchesControllerBase
{
    public override async Task<ActionResult<PromptPartPatchPageDto>> ListPromptPartPatches(
        string target_scope,
        string target_key,
        PromptPartPatchStatus? status,
        int? limit,
        string cursor
    )
    {
        try
        {
            var page = await listHandler.HandleAsync(
                new ListPromptPartPatchesQuery(
                    TargetScope: string.IsNullOrWhiteSpace(target_scope) ? null : target_scope,
                    TargetKey: string.IsNullOrWhiteSpace(target_key) ? null : target_key,
                    Status: status is null
                        ? null
                        : PromptPartPatchDtoMapper.FromStatus(status.Value),
                    Limit: limit,
                    Cursor: cursor
                ),
                HttpContext.RequestAborted
            );
            return Ok(PromptPartPatchDtoMapper.ToPageDto(page));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return UnprocessableEntity(
                ApiProblems.Build(
                    StatusCodes.Status422UnprocessableEntity,
                    "Validation failed",
                    ex.Message
                )
            );
        }
    }

    public override async Task<ActionResult<PromptPartPatchDto>> ProposePromptPartPatch(
        ProposePromptPartPatchRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var patch = await proposeHandler.HandleAsync(
            new ProposePromptPartPatchCommand(
                body.Target_scope,
                body.Target_key,
                body.Patch_text,
                body.Rationale,
                body.Base_version,
                body.Idempotency_key,
                PromptPartPatchDtoMapper.FromOperation(body.Operation),
                PromptPartPatchDtoMapper.ToDomainRoles(body.Mode_roles)),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartPatchDtoMapper.ToDto(patch));
    }

    public override async Task<ActionResult<PromptPartPatchDetailDto>> GetPromptPartPatch(
        string patch_id
    )
    {
        var view = await getHandler.HandleAsync(patch_id, HttpContext.RequestAborted);
        return Ok(PromptPartPatchDtoMapper.ToDetailDto(view));
    }

    public override async Task<ActionResult<PromptPartPatchDto>> ApplyPromptPartPatch(
        string patch_id,
        ApplyPromptPartPatchRequest body = null!
    )
    {
        var patch = await applyHandler.HandleAsync(
            new ApplyPromptPartPatchCommand(patch_id, body?.Final_text),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartPatchDtoMapper.ToDto(patch));
    }

    public override async Task<ActionResult<PromptPartPatchDto>> RejectPromptPartPatch(
        string patch_id,
        RejectPromptPartPatchRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var patch = await rejectHandler.HandleAsync(
            new RejectPromptPartPatchCommand(patch_id, body.Comment),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartPatchDtoMapper.ToDto(patch));
    }
}
