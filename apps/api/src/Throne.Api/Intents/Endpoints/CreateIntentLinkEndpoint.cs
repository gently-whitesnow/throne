using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Intents.Contracts.Generated;
using DomainIntentLinkAuthor = Throne.Domain.Intents.Linking.IntentLinkAuthor;

namespace Throne.Api.Intents;

internal static class CreateIntentLinkEndpoint
{
    public static async Task<ActionResult<IntentLinkDto>> RunAsync(string id, CreateIntentLinkRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<LinkIntentHandler>();
        try
        {
            var link = await handler.HandleAsync(
                new LinkIntentCommand(
                    id,
                    body.To_id,
                    IntentLinkDtoMapper.FromContractLinkType(body.Type),
                    DomainIntentLinkAuthor.User,
                    body.Rationale),
                http.RequestAborted);
            var location = $"/api/v1/intents/{Uri.EscapeDataString(id)}/links/{Uri.EscapeDataString(link.ToId.Value)}/{Uri.EscapeDataString(link.Type)}";
            return new CreatedResult(location, IntentLinkDtoMapper.ToLinkDto(link));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapCreateLink(ex);
        }
    }
}
