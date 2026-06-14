using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class MovePinEndpoint(MovePinHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        MovePinRequest body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var intent = await handler.HandleAsync(
            new MovePinCommand(id, body.Context_tag_id, body.Before_id, body.After_id),
            cancellationToken
        );
        return new OkObjectResult(
            await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken)
        );
    }
}
