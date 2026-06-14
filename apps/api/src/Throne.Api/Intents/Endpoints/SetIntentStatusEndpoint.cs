using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Domain.Intents.Training;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class SetIntentStatusEndpoint(
    SetIntentStatusHandler handler,
    IntentsApiHelpers helpers
)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        SetIntentStatusRequest body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var intent = await handler.HandleAsync(
            new SetIntentStatusCommand(
                id,
                IntentStatusDtoMapper.FromContractStatus(body.Status),
                body.Reason,
                IntentTrainingAuthor.User,
                "http:set_intent_status"
            ),
            cancellationToken
        );
        return new OkObjectResult(
            await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken)
        );
    }
}
