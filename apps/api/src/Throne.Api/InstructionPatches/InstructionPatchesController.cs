using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.InstructionPatches;
using Throne.InstructionPatches.Contracts.Generated;

namespace Throne.Api.InstructionPatches;

public sealed class InstructionPatchesController(
    ListInstructionPatchesHandler listHandler,
    GetInstructionPatchHandler getHandler,
    ApplyInstructionPatchHandler applyHandler,
    RejectInstructionPatchHandler rejectHandler) : InstructionPatchesControllerBase
{
    public override async Task<ActionResult<InstructionPatchPageDto>> ListInstructionPatches(
        InstructionPatchTargetKind? target_kind, InstructionPatchStatus? status, int? limit, string cursor)
    {
        try
        {
            var page = await listHandler.HandleAsync(
                new ListInstructionPatchesQuery(
                    TargetKind: target_kind is null ? null : InstructionPatchDtoMapper.FromTargetKind(target_kind.Value),
                    Status: status is null ? null : InstructionPatchDtoMapper.FromStatus(status.Value),
                    Limit: limit,
                    Cursor: cursor),
                HttpContext.RequestAborted);
            return Ok(InstructionPatchDtoMapper.ToPageDto(page));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return UnprocessableEntity(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity, "Validation failed", ex.Message));
        }
    }

    public override async Task<ActionResult<InstructionPatchDetailDto>> GetInstructionPatch(string patch_id)
    {
        try
        {
            var view = await getHandler.HandleAsync(patch_id, HttpContext.RequestAborted);
            return Ok(InstructionPatchDtoMapper.ToDetailDto(view));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.InstructionPatchNotFound)
        {
            return NotFound(ApiProblems.NotFound("InstructionPatch not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<InstructionPatchDto>> ApplyInstructionPatch(
        string patch_id, ApplyInstructionPatchRequest body = null!)
    {
        try
        {
            var patch = await applyHandler.HandleAsync(
                new ApplyInstructionPatchCommand(patch_id, body?.Final_text),
                HttpContext.RequestAborted);
            return Ok(InstructionPatchDtoMapper.ToDto(patch));
        }
        catch (ApiException ex)
        {
            return InstructionPatchesErrorMapper.MapDecision(ex);
        }
    }

    public override async Task<ActionResult<InstructionPatchDto>> RejectInstructionPatch(
        string patch_id, RejectInstructionPatchRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var patch = await rejectHandler.HandleAsync(
                new RejectInstructionPatchCommand(patch_id, body.Comment),
                HttpContext.RequestAborted);
            return Ok(InstructionPatchDtoMapper.ToDto(patch));
        }
        catch (ApiException ex)
        {
            return InstructionPatchesErrorMapper.MapDecision(ex);
        }
    }
}
