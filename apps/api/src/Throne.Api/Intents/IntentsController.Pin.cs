using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed partial class IntentsController
{
    public override async Task<ActionResult<IntentDetailDto>> PinIntent(string id, PinIntentRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await pinHandler.HandleAsync(
                new PinIntentCommand(id, body.Context_tag_id, body.Before_id, body.After_id),
                HttpContext.RequestAborted);
            return Ok(await BuildPinDetailDtoAsync(intent, HttpContext.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> UnpinIntent(string id, UnpinIntentRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await unpinHandler.HandleAsync(
                new UnpinIntentCommand(id, body.Context_tag_id),
                HttpContext.RequestAborted);
            return Ok(await BuildPinDetailDtoAsync(intent, HttpContext.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> MovePin(string id, MovePinRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await movePinHandler.HandleAsync(
                new MovePinCommand(id, body.Context_tag_id, body.Before_id, body.After_id),
                HttpContext.RequestAborted);
            return Ok(await BuildPinDetailDtoAsync(intent, HttpContext.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }

    private async Task<IntentDetailDto> BuildPinDetailDtoAsync(Intent intent, CancellationToken ct)
    {
        var tagMap = await BuildTagMapAsync(intent.TagIds, ct);
        var pinnedIn = await GetPinnedInAsync(intent.Id.Value, ct);
        return IntentDtoMapper.ToDetailDto(intent, tagMap, pinnedIn: pinnedIn);
    }

    private async Task<IReadOnlyList<string>> GetPinnedInAsync(string intentId, CancellationToken ct)
    {
        var map = await pinRepository.GetPinnedInAsync([intentId], ct);
        return map.TryGetValue(intentId, out var ids) ? ids : Array.Empty<string>();
    }
}
