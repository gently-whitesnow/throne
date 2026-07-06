using Throne.Application.Errors;

namespace Throne.Application.TaskTrackers;

/// <summary>
/// Centralised ApiException factory for the task-tracker axis. Resolving an inbound tracker key
/// against <see cref="ITaskTrackerProviderRegistry"/> and translating the miss into the shared
/// provider-unsupported problem (422) is the first server boundary that closes the open wire key
/// (ADR-0046).
/// </summary>
public static class TaskTrackerFailures
{
    public static ApiException ProviderUnsupported(
        string trackerKey,
        IEnumerable<string> supportedTrackers) =>
        new(
            ErrorCodes.TaskTrackerProviderUnsupported,
            $"Task tracker '{trackerKey}' is not supported on this Throne build.",
            new Dictionary<string, object?>
            {
                ["tracker"] = trackerKey,
                ["supported_trackers"] = supportedTrackers.ToArray(),
            });

    public static ApiException ConnectionMissing(string trackerKey) =>
        new(
            ErrorCodes.TaskTrackerConnectionMissing,
            $"No connection is configured for task tracker '{trackerKey}'.",
            new Dictionary<string, object?> { ["tracker"] = trackerKey });

    public static ApiException ConnectionRejected(string trackerKey, string detail) =>
        new(
            ErrorCodes.TaskTrackerConnectionRejected,
            $"The saved '{trackerKey}' token was rejected — reconnect the tracker: {detail}",
            new Dictionary<string, object?> { ["tracker"] = trackerKey });

    public static ApiException ConnectionBlocked(string trackerKey, string detail) =>
        new(
            ErrorCodes.TaskTrackerConnectionBlocked,
            $"The '{trackerKey}' tracker refused the request on tariff-plan grounds (402): {detail}",
            new Dictionary<string, object?> { ["tracker"] = trackerKey });

    public static ApiException UpstreamUnavailable(string trackerKey, string detail) =>
        new(
            ErrorCodes.TaskTrackerUpstreamUnavailable,
            $"The '{trackerKey}' task-tracker API could not be reached: {detail}",
            new Dictionary<string, object?> { ["tracker"] = trackerKey });
}
