using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/links* and /api/v1/intents/links/summary.
/// Split from <see cref="IntentsController"/> so each tag-scoped controller stays
/// under the CA1502 cyclomatic budget. Bodies live in per-endpoint instances
/// (CreateIntentLinkEndpoint, DeleteIntentLinkEndpoint, ListIntentLinksEndpoint,
/// GetIntentLinksSummaryEndpoint) injected via ctor.
/// </summary>
public sealed class IntentLinksController(
    CreateIntentLinkEndpoint createIntentLink,
    DeleteIntentLinkEndpoint deleteIntentLink,
    ListIntentLinksEndpoint listIntentLinks,
    GetIntentLinksSummaryEndpoint getIntentLinksSummary) : IntentLinksControllerBase
{
    public override Task<ActionResult<IntentLinkDto>> CreateIntentLink(string id, CreateIntentLinkRequest body) =>
        createIntentLink.RunAsync(id, body, HttpContext.RequestAborted);

    public override Task<IActionResult> DeleteIntentLink(string id, string to_id) =>
        deleteIntentLink.RunAsync(id, to_id, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentLinksPageDto>> ListIntentLinks(
        string id,
        ContractIntentLinkDirection? direction = null,
        bool? blocking = null,
        int? limit = null,
        string cursor = null!) =>
        listIntentLinks.RunAsync(id, direction, blocking, limit, cursor, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentLinksSummaryDto>> GetIntentLinksSummary(IEnumerable<string> ids) =>
        getIntentLinksSummary.RunAsync(ids, HttpContext.RequestAborted);
}
