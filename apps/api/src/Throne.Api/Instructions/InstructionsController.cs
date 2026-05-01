using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Domain.Instructions;
using Throne.Instructions.Contracts.Generated;

namespace Throne.Api.Instructions;

public sealed class InstructionsController(
    ListInstructionsHandler listHandler,
    GetInstructionHandler getHandler) : InstructionsControllerBase
{
    private const int TextShortMaxLength = 140;

    public override async Task<ActionResult<ICollection<InstructionListItemDto>>> ListInstructions()
    {
        var instructions = await listHandler.HandleAsync(new ListInstructionsQuery(), HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var dtos = new List<InstructionListItemDto>(instructions.Count);
        foreach (var instruction in instructions)
        {
            dtos.Add(ToListDto(instruction));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<InstructionDetailDto>> GetInstruction(string id)
    {
        try
        {
            var instruction = await getHandler.HandleAsync(new GetInstructionQuery(id), HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return Ok(ToDetailDto(instruction));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.InstructionNotFound)
        {
            return NotFound(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = "about:blank",
                Title = "Instruction not found",
                Status = StatusCodes.Status404NotFound,
                Detail = ex.Detail,
            });
        }
    }

    private static InstructionListItemDto ToListDto(Instruction instruction) => new()
    {
        Id = instruction.Id.Value,
        Kind = instruction.Kind,
        Current_version = instruction.CurrentVersion,
        Text_short = TextShort(instruction.Text),
        Created_at = instruction.CreatedAt,
        Updated_at = instruction.UpdatedAt,
    };

    private static InstructionDetailDto ToDetailDto(Instruction instruction) => new()
    {
        Id = instruction.Id.Value,
        Kind = instruction.Kind,
        Current_version = instruction.CurrentVersion,
        Text = instruction.Text,
        Created_at = instruction.CreatedAt,
        Updated_at = instruction.UpdatedAt,
    };

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
