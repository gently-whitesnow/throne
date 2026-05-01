using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class IntentsController(ListIntentsHandler listHandler) : IntentsControllerBase
{
    private const int TextShortMaxLength = 140;

    public override async Task<ActionResult<ICollection<IntentListItemDto>>> ListIntents()
    {
        var intents = await listHandler.HandleAsync(new ListIntentsQuery(), HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var dtos = new List<IntentListItemDto>(intents.Count);
        foreach (var intent in intents)
        {
            dtos.Add(ToDto(intent));
        }
        return Ok(dtos);
    }

    private static IntentListItemDto ToDto(Intent intent) => new()
    {
        Id = intent.Id.Value,
        Current_version = intent.CurrentVersion,
        Tags = [.. intent.Tags],
        Text_short = TextShort(intent.Text),
        Created_at = intent.CreatedAt,
        Updated_at = intent.UpdatedAt,
    };

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
