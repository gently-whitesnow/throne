using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Intents.Contracts.Generated;
using DomainIntentLinkAuthor = Throne.Domain.Intents.Linking.IntentLinkAuthor;

namespace Throne.Api.Intents;

public sealed class CreateIntentLinkEndpoint(LinkIntentHandler handler)
{
    public async Task<ActionResult<IntentLinkDto>> RunAsync(
        string id,
        CreateIntentLinkRequest body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var link = await handler.HandleAsync(
            new LinkIntentCommand(
                id,
                body.To_id,
                body.Blocking,
                DomainIntentLinkAuthor.User,
                body.Rationale
            ),
            cancellationToken
        );
        var location =
            $"/api/v1/intents/{Uri.EscapeDataString(id)}/links/{Uri.EscapeDataString(link.ToId.Value)}";
        return new CreatedResult(location, IntentLinkDtoMapper.ToLinkDto(link));
    }
}
