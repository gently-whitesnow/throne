using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/links* and /api/v1/intents/links/summary.
/// Split from <see cref="IntentsController"/> so each tag-scoped controller stays
/// under the CA1502 cyclomatic budget. Bodies live in per-endpoint static helpers
/// (CreateIntentLinkEndpoint, DeleteIntentLinkEndpoint, ListIntentLinksEndpoint,
/// GetIntentLinksSummaryEndpoint).
/// </summary>
public sealed class IntentLinksController : IntentLinksControllerBase
{
    public override Task<ActionResult<IntentLinkDto>> CreateIntentLink(string id, CreateIntentLinkRequest body) =>
        CreateIntentLinkEndpoint.RunAsync(id, body, HttpContext);

    public override Task<IActionResult> DeleteIntentLink(string id, string to_id, ContractIntentLinkType type) =>
        DeleteIntentLinkEndpoint.RunAsync(id, to_id, type, HttpContext);

    public override Task<ActionResult<IntentLinksPageDto>> ListIntentLinks(
        string id,
        ContractIntentLinkDirection? direction = null,
        ContractIntentLinkType? type = null,
        int? limit = null,
        string cursor = null!) =>
        ListIntentLinksEndpoint.RunAsync(id, direction, type, limit, cursor, HttpContext);

    public override Task<ActionResult<IntentLinksSummaryDto>> GetIntentLinksSummary(IEnumerable<string> ids) =>
        GetIntentLinksSummaryEndpoint.RunAsync(ids, HttpContext);
}
