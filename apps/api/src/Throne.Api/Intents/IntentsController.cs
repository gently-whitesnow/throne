using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.TextVersions;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class IntentsController(
    ListIntentsHandler listHandler,
    GetIntentHandler getHandler,
    CreateIntentHandler createHandler,
    ReplaceIntentTextHandler replaceHandler,
    DeleteIntentHandler deleteHandler,
    ListIntentVersionsHandler listVersionsHandler) : IntentsControllerBase
{
    private const int TextShortMaxLength = 140;

    public override async Task<ActionResult<ICollection<IntentListItemDto>>> ListIntents()
    {
        var intents = await listHandler.HandleAsync(new ListIntentsQuery(), HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var dtos = new List<IntentListItemDto>(intents.Count);
        foreach (var intent in intents)
        {
            dtos.Add(ToListDto(intent));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<IntentDetailDto>> GetIntent(string id)
    {
        try
        {
            var intent = await getHandler.HandleAsync(new GetIntentQuery(id), HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return Ok(ToDetailDto(intent));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(NotFoundProblem("Intent not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> CreateIntent(CreateIntentRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var intent = await createHandler.HandleAsync(
            new CreateIntentCommand(body.Text, body.Tags?.ToList(), TextVersionAuthor.User),
            HttpContext.RequestAborted).ConfigureAwait(false);

        var dto = ToDetailDto(intent);
        return CreatedAtAction(nameof(GetIntent), new { id = intent.Id.Value }, dto);
    }

    public override async Task<ActionResult<IntentDetailDto>> ReplaceIntentText(string id, ReplaceTextRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var intent = await replaceHandler.HandleAsync(
                new ReplaceIntentTextCommand(id, body.Expected_version, body.Old_text, body.New_text, TextVersionAuthor.User),
                HttpContext.RequestAborted).ConfigureAwait(false);
            return Ok(ToDetailDto(intent));
        }
        catch (ApiException ex)
        {
            return MapReplaceError(ex);
        }
    }

    public override async Task<IActionResult> DeleteIntent(string id)
    {
        try
        {
            await deleteHandler.HandleAsync(new DeleteIntentCommand(id), HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(NotFoundProblem("Intent not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<ICollection<TextVersionDto>>> ListIntentVersions(string id)
    {
        try
        {
            var versions = await listVersionsHandler.HandleAsync(
                new ListIntentVersionsQuery(id), HttpContext.RequestAborted).ConfigureAwait(false);

            var dtos = new List<TextVersionDto>(versions.Count);
            foreach (var v in versions)
            {
                dtos.Add(ToVersionDto(v));
            }
            return Ok(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(NotFoundProblem("Intent not found", ex.Detail));
        }
    }

    private ActionResult<IntentDetailDto> MapReplaceError(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound => NotFound(NotFoundProblem("Intent not found", ex.Detail)),
            ErrorCodes.IntentVersionConflict => Conflict(BuildProblem(
                StatusCodes.Status409Conflict, "Intent version conflict", ex)),
            ErrorCodes.IntentTextMatchNotFound or ErrorCodes.IntentTextMatchAmbiguous =>
                UnprocessableEntity(BuildProblem(StatusCodes.Status422UnprocessableEntity, "Intent text match error", ex)),
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

    private static IntentListItemDto ToListDto(Intent intent) => new()
    {
        Id = intent.Id.Value,
        Current_version = intent.CurrentVersion,
        Tags = [.. intent.Tags],
        Text_short = TextShort(intent.Text),
        Created_at = intent.CreatedAt,
        Updated_at = intent.UpdatedAt,
    };

    private static IntentDetailDto ToDetailDto(Intent intent) => new()
    {
        Id = intent.Id.Value,
        Current_version = intent.CurrentVersion,
        Tags = [.. intent.Tags],
        Text = intent.Text,
        Created_at = intent.CreatedAt,
        Updated_at = intent.UpdatedAt,
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
