using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/pin* — pin / unpin / move-pin endpoints.
/// Split from <see cref="IntentsController"/> so each tag-scoped controller stays
/// under the CA1502 cyclomatic budget. Bodies live in per-endpoint static helpers
/// (PinIntentEndpoint, UnpinIntentEndpoint, MovePinEndpoint).
/// </summary>
public sealed class IntentPinsController : IntentPinsControllerBase
{
    public override Task<ActionResult<IntentDetailDto>> PinIntent(string id, PinIntentRequest body) =>
        PinIntentEndpoint.RunAsync(id, body, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> UnpinIntent(string id, UnpinIntentRequest body) =>
        UnpinIntentEndpoint.RunAsync(id, body, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> MovePin(string id, MovePinRequest body) =>
        MovePinEndpoint.RunAsync(id, body, HttpContext);
}
