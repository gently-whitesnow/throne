using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class CreateIntentEndpoint(CreateIntentHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        CreateIntentRequest body,
        IUrlHelper url,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var intent = await handler.HandleAsync(
            new CreateIntentCommand(body.Text, body.Tag_names?.ToList(), TextVersionAuthor.User), cancellationToken);
        var tagMap = await helpers.BuildTagMapAsync(intent.TagIds, cancellationToken);
        // Freshly created intents start unpinned; skip the pin lookup.
        var dto = IntentDtoMapper.ToDetailDto(intent, tagMap, pinnedIn: Array.Empty<IntentPin>());
        var location = url.Action(nameof(IntentsController.GetIntent), new { id = intent.Id.Value });
        return new CreatedResult(location, dto);
    }
}
