using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class ReplaceIntentTextEndpoint(
    ReplaceIntentTextHandler handler,
    IntentsApiHelpers helpers
)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        ReplaceTextRequest body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var intent = await handler.HandleAsync(
            new ReplaceIntentTextCommand(
                id,
                body.Expected_version,
                body.Old_text,
                body.New_text,
                TextVersionAuthor.User
            ),
            cancellationToken
        );
        return new OkObjectResult(
            await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken)
        );
    }
}
