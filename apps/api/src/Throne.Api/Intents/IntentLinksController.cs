using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/links* and /api/v1/intents/links/summary.
/// One tag-scoped controller per intent sub-resource; bodies live in per-endpoint
/// instances (CreateIntentLinkEndpoint, DeleteIntentLinkEndpoint, ListIntentLinksEndpoint,
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

    public override Task<IActionResult> DeleteIntentLink(string id, string to_id, ContractIntentLinkType type) =>
        deleteIntentLink.RunAsync(id, to_id, type, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentLinksPageDto>> ListIntentLinks(
        string id,
        ContractIntentLinkDirection? direction = null,
        ContractIntentLinkType? type = null,
        int? limit = null,
        string cursor = null!) =>
        listIntentLinks.RunAsync(id, direction, type, limit, cursor, HttpContext.RequestAborted);

    public override Task<ActionResult<IntentLinksSummaryDto>> GetIntentLinksSummary(IEnumerable<string> ids) =>
        getIntentLinksSummary.RunAsync(ids, HttpContext.RequestAborted);
}
