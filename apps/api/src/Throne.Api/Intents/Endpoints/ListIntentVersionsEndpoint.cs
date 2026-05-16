using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class ListIntentVersionsEndpoint
{
    public static async Task<ActionResult<ICollection<TextVersionDto>>> RunAsync(string id, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<ListIntentVersionsHandler>();
        try
        {
            var versions = await handler.HandleAsync(new ListIntentVersionsQuery(id), http.RequestAborted);
            return new OkObjectResult(versions.Select(TextVersionDtoMapper.ToDto).ToList());
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }
}
