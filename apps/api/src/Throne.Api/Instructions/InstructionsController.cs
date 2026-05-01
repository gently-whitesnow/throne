using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.TextVersions;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;
using Throne.Instructions.Contracts.Generated;

namespace Throne.Api.Instructions;

public sealed class InstructionsController(
    ListInstructionsHandler listHandler,
    GetInstructionHandler getHandler,
    ReplaceInstructionTextHandler replaceHandler,
    ListInstructionVersionsHandler listVersionsHandler) : InstructionsControllerBase
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
            return NotFound(NotFoundProblem("Instruction not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<InstructionDetailDto>> ReplaceInstructionText(string id, ReplaceTextRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var instruction = await replaceHandler.HandleAsync(
                new ReplaceInstructionTextCommand(id, body.Expected_version, body.Old_text, body.New_text, TextVersionAuthor.User),
                HttpContext.RequestAborted).ConfigureAwait(false);
            return Ok(ToDetailDto(instruction));
        }
        catch (ApiException ex)
        {
            return MapReplaceError(ex);
        }
    }

    public override async Task<ActionResult<ICollection<TextVersionDto>>> ListInstructionVersions(string id)
    {
        try
        {
            var versions = await listVersionsHandler.HandleAsync(
                new ListInstructionVersionsQuery(id), HttpContext.RequestAborted).ConfigureAwait(false);

            var dtos = new List<TextVersionDto>(versions.Count);
            foreach (var v in versions)
            {
                dtos.Add(ToVersionDto(v));
            }
            return Ok(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.InstructionNotFound)
        {
            return NotFound(NotFoundProblem("Instruction not found", ex.Detail));
        }
    }

    private ActionResult<InstructionDetailDto> MapReplaceError(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.InstructionNotFound => NotFound(NotFoundProblem("Instruction not found", ex.Detail)),
            ErrorCodes.InstructionVersionConflict => Conflict(BuildProblem(
                StatusCodes.Status409Conflict, "Instruction version conflict", ex)),
            ErrorCodes.InstructionTextMatchNotFound or ErrorCodes.InstructionTextMatchAmbiguous =>
                UnprocessableEntity(BuildProblem(StatusCodes.Status422UnprocessableEntity, "Instruction text match error", ex)),
            ErrorCodes.ValidationFailed => UnprocessableEntity(BuildProblem(
                StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw ex,
        };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails NotFoundProblem(string title, string detail) => new()
    {
        Type = "about:blank",
        Title = title,
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
    };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails BuildProblem(int status, string title, ApiException ex)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Detail = ex.Detail,
        };
        problem.Extensions["code"] = ex.Code;
        foreach (var (key, value) in ex.Extensions)
        {
            problem.Extensions[key] = value;
        }
        return problem;
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

    private static TextVersionDto ToVersionDto(TextVersion v) => new()
    {
        Version = v.Version,
        Kind = v.Kind switch
        {
            TextVersionKind.Create => TextVersionDtoKind.Create,
            TextVersionKind.Replace => TextVersionDtoKind.Replace,
            TextVersionKind.Insert => TextVersionDtoKind.Insert,
            _ => throw new InvalidOperationException($"Unknown kind: {v.Kind}"),
        },
        Changed_at = v.ChangedAt,
        Changed_by = v.ChangedBy switch
        {
            TextVersionAuthor.User => TextVersionDtoChanged_by.User,
            TextVersionAuthor.Agent => TextVersionDtoChanged_by.Agent,
            TextVersionAuthor.System => TextVersionDtoChanged_by.System,
            _ => throw new InvalidOperationException($"Unknown author: {v.ChangedBy}"),
        },
        Snapshot = v.Snapshot,
        Old_text = v.OldText,
        New_text = v.NewText,
        After_line = v.AfterLine ?? 0,
        Insert_text = v.InsertText,
    };

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
