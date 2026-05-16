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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var link = await handler.HandleAsync(
                new LinkIntentCommand(
                    id,
                    body.To_id,
                    IntentLinkDtoMapper.FromContractLinkType(body.Type),
                    DomainIntentLinkAuthor.User,
                    body.Rationale),
                cancellationToken);
            var location = $"/api/v1/intents/{Uri.EscapeDataString(id)}/links/{Uri.EscapeDataString(link.ToId.Value)}/{Uri.EscapeDataString(link.Type)}";
            return new CreatedResult(location, IntentLinkDtoMapper.ToLinkDto(link));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapCreateLink(ex);
        }
    }
}
