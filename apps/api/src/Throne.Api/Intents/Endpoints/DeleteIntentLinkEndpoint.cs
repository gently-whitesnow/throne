using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Intents.Linking;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;

namespace Throne.Api.Intents;

internal static class DeleteIntentLinkEndpoint
{
    public static async Task<IActionResult> RunAsync(string id, string toId, ContractIntentLinkType type, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<UnlinkIntentHandler>();
        await handler.HandleAsync(
            new UnlinkIntentCommand(id, toId, IntentLinkDtoMapper.FromContractLinkType(type)),
            http.RequestAborted);
        return new NoContentResult();
    }
}
