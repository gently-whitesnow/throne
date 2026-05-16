using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Application.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// Trampoline facade for <see cref="IntentsController"/>: groups the 10 core
/// endpoint entry points and forwards each to a focused per-endpoint static
/// helper. Keeping every facade method a one-line forward (CC = 1) keeps the
/// type's cumulative cyclomatic complexity within the CA1502 type-level budget
/// (≤10). Real work lives in the per-endpoint helpers (ListIntentsEndpoint, …).
/// </summary>
internal static class IntentsCoreEndpoints
{
    public static Task<ActionResult<ICollection<IntentListItemDto>>> ListAsync(IEnumerable<IntentStatus>? status, HttpContext http) =>
        ListIntentsEndpoint.RunAsync(status, http);

    public static Task<ActionResult<ICollection<IntentEventDto>>> ListEventsAsync(string id, HttpContext http) =>
        ListIntentEventsEndpoint.RunAsync(id, http);

    public static Task<ActionResult<IntentDetailDto>> GetAsync(string id, HttpContext http) =>
        GetIntentEndpoint.RunAsync(id, http);

    public static Task<ActionResult<IntentDetailDto>> CreateAsync(CreateIntentRequest body, HttpContext http, IUrlHelper url) =>
        CreateIntentEndpoint.RunAsync(body, http, url);

    public static Task<ActionResult<IntentDetailDto>> SetTagsAsync(string id, SetIntentTagsRequest body, HttpContext http) =>
        SetIntentTagsEndpoint.RunAsync(id, body, http);

    public static Task<ActionResult<IntentDetailDto>> ReplaceTextAsync(string id, ReplaceTextRequest body, HttpContext http) =>
        ReplaceIntentTextEndpoint.RunAsync(id, body, http);

    public static Task<ActionResult<IntentDetailDto>> SetStatusAsync(string id, SetIntentStatusRequest body, HttpContext http) =>
        SetIntentStatusEndpoint.RunAsync(id, body, http);

    public static Task<ActionResult<IntentDetailDto>> MoveAsync(string id, MoveIntentRequest body, HttpContext http) =>
        MoveIntentEndpoint.RunAsync(id, body, http);

    public static Task<IActionResult> DeleteAsync(string id, HttpContext http) =>
        DeleteIntentEndpoint.RunAsync(id, http);

    public static Task<ActionResult<ICollection<TextVersionDto>>> ListVersionsAsync(string id, HttpContext http) =>
        ListIntentVersionsEndpoint.RunAsync(id, http);
}
