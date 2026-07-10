using System.Net;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;

namespace Throne.Infrastructure.TaskTrackers;

/// <summary>
/// Kaiten HTTP status → connection-health taxonomy + the board-read failure surface for the
/// settings axis (auth reconnect, blocked tariff, offline transient). Extracted from
/// <see cref="KaitenTaskTrackerProvider"/> so the provider file stays inside the maintainability
/// budget.
/// </summary>
internal static class KaitenTaskTrackerFailureMap
{
    /// <summary>
    /// 401/403 → auth (reconnect), 402 → blocked (tariff), everything else — 5xx, an unexpected
    /// status — → offline (transient, keep the binding). A 404 is «gone» at the call site.
    /// </summary>
    public static TaskTrackerConnectionHealth Classify(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TaskTrackerConnectionHealth.Auth,
        HttpStatusCode.PaymentRequired => TaskTrackerConnectionHealth.Blocked,
        _ => TaskTrackerConnectionHealth.Offline,
    };

    public static ApiException BoardReadFailure(string trackerKey, TaskTrackerConnectionHealth health, string detail) => health switch
    {
        TaskTrackerConnectionHealth.Auth => TaskTrackerFailures.ConnectionRejected(trackerKey, detail),
        TaskTrackerConnectionHealth.Blocked => TaskTrackerFailures.ConnectionBlocked(trackerKey, detail),
        _ => TaskTrackerFailures.UpstreamUnavailable(trackerKey, detail),
    };

    public static bool IsGone(HttpStatusCode status) => status is HttpStatusCode.NotFound;
}
