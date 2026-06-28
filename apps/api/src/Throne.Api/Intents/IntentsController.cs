using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for the core /api/v1/intents/* surface (CRUD + status / tags / move / versions / events).
/// Methods are 1-line trampolines into per-endpoint instances injected through the
/// primary constructor; each endpoint owns its own handler / helper dependencies.
/// Companion controllers cover pin (IntentPinsController), link (IntentLinksController)
/// and attachment (IntentAttachmentsController) sub-surfaces; the four-way split mirrors
/// the OpenAPI tag groups (Intents / IntentPins / IntentLinks / IntentAttachments).
/// </summary>
public sealed class IntentsController(
    ListIntentsEndpoint listIntents,
    GetIntentContextsEndpoint getIntentContexts,
    ListIntentEventsEndpoint listIntentEvents,
    GetIntentEndpoint getIntent,
    CreateIntentEndpoint createIntent,
    SetIntentTagsEndpoint setIntentTags,
    SetIntentTitleEndpoint setIntentTitle,
    ReplaceIntentTextEndpoint replaceIntentText,
    SetIntentStatusEndpoint setIntentStatus,
    SetIntentCleanupOnDoneEndpoint setIntentCleanupOnDone,
    MoveIntentEndpoint moveIntent,
    DeleteIntentEndpoint deleteIntent,
    ListIntentVersionsEndpoint listIntentVersions) : IntentsControllerBase
{
    public override Task<ActionResult<IntentListPageDto>> ListIntents(
        string cursor = null!,
        int? limit = null,
        IEnumerable<IntentStatus> status = null!,
        string tag = null!,
        bool? untagged = null,
        bool? pinned = null,
        bool? terminal_running = null,
        string query = null!,
        IntentListSort? sort = null) =>
        listIntents.RunAsync(
            cursor, limit, status, tag, untagged, pinned, terminal_running, query, sort, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentContextCountsDto>> GetIntentContexts() =>
        getIntentContexts.RunAsync(HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<IntentEventDto>>> ListIntentEvents(string id) =>
        listIntentEvents.RunAsync(id, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> GetIntent(string id) =>
        getIntent.RunAsync(id, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> CreateIntent(CreateIntentRequest body) =>
        createIntent.RunAsync(body, Url, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> SetIntentTags(string id, SetIntentTagsRequest body) =>
        setIntentTags.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> SetIntentTitle(string id, SetIntentTitleRequest body) =>
        setIntentTitle.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> ReplaceIntentText(string id, ReplaceTextRequest body) =>
        replaceIntentText.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> SetIntentStatus(string id, SetIntentStatusRequest body) =>
        setIntentStatus.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> SetIntentCleanupOnDone(
        string id, SetIntentCleanupOnDoneRequest body) =>
        setIntentCleanupOnDone.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentDetailDto>> MoveIntent(string id, MoveIntentRequest body) =>
        moveIntent.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<IActionResult> DeleteIntent(string id) =>
        deleteIntent.RunAsync(id, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<TextVersionDto>>> ListIntentVersions(string id) =>
        listIntentVersions.RunAsync(id, HttpContext.RequestAborted);
}
