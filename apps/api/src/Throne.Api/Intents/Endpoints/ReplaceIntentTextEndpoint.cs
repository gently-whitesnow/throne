using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class ReplaceIntentTextEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, ReplaceTextRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<ReplaceIntentTextHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(
                new ReplaceIntentTextCommand(id, body.Expected_version, body.Old_text, body.New_text, TextVersionAuthor.User),
                http.RequestAborted);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, http.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapReplace(ex);
        }
    }
}
