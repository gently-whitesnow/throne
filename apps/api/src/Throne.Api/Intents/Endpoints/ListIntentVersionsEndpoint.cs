using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class ListIntentVersionsEndpoint(ListIntentVersionsHandler handler)
{
    public async Task<ActionResult<ICollection<TextVersionDto>>> RunAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var versions = await handler.HandleAsync(new ListIntentVersionsQuery(id), cancellationToken);
            return new OkObjectResult(versions.Select(TextVersionDtoMapper.ToDto).ToList());
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }
}
