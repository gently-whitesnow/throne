using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.TextVersions;
using Throne.Domain.TextVersions;
using Throne.Instructions.Contracts.Generated;

namespace Throne.Api.Instructions;

public sealed class InstructionsController(
    ListInstructionsHandler listHandler,
    GetInstructionHandler getHandler,
    ReplaceInstructionTextHandler replaceHandler,
    CreateInstructionHandler createHandler,
    ListInstructionVersionsHandler listVersionsHandler,
    GetBundlesTreeHandler bundlesTreeHandler) : InstructionsControllerBase
{
    public override async Task<ActionResult<BundlesTreeDto>> GetBundlesTree()
    {
        var tree = await bundlesTreeHandler.HandleAsync(new GetBundlesTreeQuery(), HttpContext.RequestAborted);
        return Ok(BundlesTreeDtoMapper.ToDto(tree));
    }

    public override async Task<ActionResult<ICollection<InstructionListItemDto>>> ListInstructions()
    {
        var instructions = await listHandler.HandleAsync(new ListInstructionsQuery(), HttpContext.RequestAborted);
        var dtos = new List<InstructionListItemDto>(instructions.Count);
        foreach (var instruction in instructions)
        {
            dtos.Add(InstructionDtoMapper.ToListDto(instruction));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<InstructionDetailDto>> CreateInstruction(CreateInstructionRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var instruction = await createHandler.HandleAsync(
                new CreateInstructionCommand(body.Kind, body.Text),
                HttpContext.RequestAborted);
            return Ok(InstructionDtoMapper.ToDetailDto(instruction));
        }
        catch (ApiException ex)
        {
            return InstructionsErrorMapper.MapCreate(ex);
        }
    }

    public override async Task<ActionResult<InstructionDetailDto>> GetInstruction(string id)
    {
        try
        {
            var instruction = await getHandler.HandleAsync(new GetInstructionQuery(id), HttpContext.RequestAborted);
            return Ok(InstructionDtoMapper.ToDetailDto(instruction));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.InstructionNotFound)
        {
            return NotFound(ApiProblems.NotFound("Instruction not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<InstructionDetailDto>> ReplaceInstructionText(string id, ReplaceTextRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var instruction = await replaceHandler.HandleAsync(
                new ReplaceInstructionTextCommand(id, body.Expected_version, body.Old_text, body.New_text, TextVersionAuthor.User),
                HttpContext.RequestAborted);
            return Ok(InstructionDtoMapper.ToDetailDto(instruction));
        }
        catch (ApiException ex)
        {
            return InstructionsErrorMapper.MapReplace(ex);
        }
    }

    public override async Task<ActionResult<ICollection<TextVersionDto>>> ListInstructionVersions(string id)
    {
        try
        {
            var versions = await listVersionsHandler.HandleAsync(
                new ListInstructionVersionsQuery(id), HttpContext.RequestAborted);
            var dtos = new List<TextVersionDto>(versions.Count);
            foreach (var v in versions)
            {
                dtos.Add(TextVersionDtoMapper.ToDto(v));
            }
            return Ok(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.InstructionNotFound)
        {
            return NotFound(ApiProblems.NotFound("Instruction not found", ex.Detail));
        }
    }
}
