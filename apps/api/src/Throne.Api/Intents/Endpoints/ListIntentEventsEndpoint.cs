using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents.Events;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class ListIntentEventsEndpoint(ListIntentEventsHandler handler)
{
    public async Task<ActionResult<ICollection<IntentEventDto>>> RunAsync(
        string id,
        CancellationToken cancellationToken
    )
    {
        var events = await handler.HandleAsync(new ListIntentEventsQuery(id), cancellationToken);
        return new OkObjectResult(events.Select(IntentEventDtoMapper.ToEventDto).ToList());
    }
}
