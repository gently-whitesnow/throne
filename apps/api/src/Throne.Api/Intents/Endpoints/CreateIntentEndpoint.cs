using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class CreateIntentEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(CreateIntentRequest body, HttpContext http, IUrlHelper url)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<CreateIntentHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        var intent = await handler.HandleAsync(
            new CreateIntentCommand(body.Text, body.Tag_names?.ToList(), TextVersionAuthor.User), http.RequestAborted);
        var tagMap = await helpers.BuildTagMapAsync(intent.TagIds, http.RequestAborted);
        // Freshly created intents start unpinned; skip the pin lookup.
        var dto = IntentDtoMapper.ToDetailDto(intent, tagMap, pinnedIn: Array.Empty<IntentPin>());
        var location = url.Action(nameof(IntentsController.GetIntent), new { id = intent.Id.Value });
        return new CreatedResult(location, dto);
    }
}
