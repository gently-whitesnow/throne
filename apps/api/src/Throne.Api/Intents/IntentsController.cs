using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class IntentsController(
    ListIntentsHandler listHandler,
    GetIntentHandler getHandler) : IntentsControllerBase
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
            return NotFound(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = "about:blank",
                Title = "Intent not found",
                Status = StatusCodes.Status404NotFound,
                Detail = ex.Detail,
            });
        }
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

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
