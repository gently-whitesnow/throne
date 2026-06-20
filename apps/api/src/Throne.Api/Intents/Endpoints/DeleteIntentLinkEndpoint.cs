using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents.Linking;

namespace Throne.Api.Intents;

public sealed class DeleteIntentLinkEndpoint(UnlinkIntentHandler handler)
{
    public async Task<IActionResult> RunAsync(
        string id,
        string toId,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new UnlinkIntentCommand(id, toId),
            cancellationToken);
        return new NoContentResult();
    }
}
