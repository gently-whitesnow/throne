using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class SetIntentTagsEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, SetIntentTagsRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<SetIntentTagsHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(
                new SetIntentTagsCommand(id, body.Expected_version, TagIds: null, body.Tag_names?.ToList()),
                http.RequestAborted);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, http.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapSetTags(ex);
        }
    }
}
