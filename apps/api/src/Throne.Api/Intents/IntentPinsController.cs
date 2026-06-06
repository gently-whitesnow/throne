using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/pin* — pin / unpin / move-pin endpoints.
/// One tag-scoped controller per intent sub-resource; bodies live in per-endpoint
/// instances (PinIntentEndpoint, UnpinIntentEndpoint, MovePinEndpoint) injected via ctor.
/// </summary>
public sealed class IntentPinsController(
    PinIntentEndpoint pinIntent,
    UnpinIntentEndpoint unpinIntent,
    MovePinEndpoint movePin) : IntentPinsControllerBase
{
    public override Task<ActionResult<IntentDetailDto>> PinIntent(string id, PinIntentRequest body) =>
        pinIntent.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> UnpinIntent(string id, UnpinIntentRequest body) =>
        unpinIntent.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> MovePin(string id, MovePinRequest body) =>
        movePin.RunAsync(id, body, HttpContext.RequestAborted);
}
