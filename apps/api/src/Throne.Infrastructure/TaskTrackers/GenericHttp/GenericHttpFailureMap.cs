using System.Net;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;

namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal static class GenericHttpFailureMap
{
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
