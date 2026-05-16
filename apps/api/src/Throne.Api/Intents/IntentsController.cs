using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for the core /api/v1/intents/* surface (CRUD + status / tags / move / versions / events).
/// Methods are 1-line trampolines into <see cref="IntentsCoreEndpoints"/> so the
/// controller type stays well under the CA1502 type-level cyclomatic budget.
/// Companion controllers cover pin (IntentPinsController), link (IntentLinksController)
/// and attachment (IntentAttachmentsController) sub-surfaces; the four-way split mirrors
/// the OpenAPI tag groups (Intents / IntentPins / IntentLinks / IntentAttachments).
/// </summary>
public sealed class IntentsController : IntentsControllerBase
{
    public override Task<ActionResult<ICollection<IntentListItemDto>>> ListIntents(IEnumerable<IntentStatus> status = null!) =>
        IntentsCoreEndpoints.ListAsync(status, HttpContext);

    public override Task<ActionResult<ICollection<IntentEventDto>>> ListIntentEvents(string id) =>
        IntentsCoreEndpoints.ListEventsAsync(id, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> GetIntent(string id) =>
        IntentsCoreEndpoints.GetAsync(id, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> CreateIntent(CreateIntentRequest body) =>
        IntentsCoreEndpoints.CreateAsync(body, HttpContext, Url);

    public override Task<ActionResult<IntentDetailDto>> SetIntentTags(string id, SetIntentTagsRequest body) =>
        IntentsCoreEndpoints.SetTagsAsync(id, body, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> ReplaceIntentText(string id, ReplaceTextRequest body) =>
        IntentsCoreEndpoints.ReplaceTextAsync(id, body, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> SetIntentStatus(string id, SetIntentStatusRequest body) =>
        IntentsCoreEndpoints.SetStatusAsync(id, body, HttpContext);

    public override Task<ActionResult<IntentDetailDto>> MoveIntent(string id, MoveIntentRequest body) =>
        IntentsCoreEndpoints.MoveAsync(id, body, HttpContext);

    public override Task<IActionResult> DeleteIntent(string id) =>
        IntentsCoreEndpoints.DeleteAsync(id, HttpContext);

    public override Task<ActionResult<ICollection<TextVersionDto>>> ListIntentVersions(string id) =>
        IntentsCoreEndpoints.ListVersionsAsync(id, HttpContext);
}
