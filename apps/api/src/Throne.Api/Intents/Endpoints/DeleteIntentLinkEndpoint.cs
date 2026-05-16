using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents.Linking;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;

namespace Throne.Api.Intents;

public sealed class DeleteIntentLinkEndpoint(UnlinkIntentHandler handler)
{
    public async Task<IActionResult> RunAsync(
        string id,
        string toId,
        ContractIntentLinkType type,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new UnlinkIntentCommand(id, toId, IntentLinkDtoMapper.FromContractLinkType(type)),
            cancellationToken);
        return new NoContentResult();
    }
}
