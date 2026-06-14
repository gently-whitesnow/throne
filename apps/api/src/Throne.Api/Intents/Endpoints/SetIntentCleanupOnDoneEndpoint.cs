using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class SetIntentCleanupOnDoneEndpoint(
    SetIntentCleanupOnDoneHandler handler,
    IntentsApiHelpers helpers
)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        SetIntentCleanupOnDoneRequest body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var intent = await handler.HandleAsync(
            new SetIntentCleanupOnDoneCommand(id, body.Cleanup_local_state_on_done),
            cancellationToken
        );
        return new OkObjectResult(
            await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken)
        );
    }
}
