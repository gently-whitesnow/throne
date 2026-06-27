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
}
