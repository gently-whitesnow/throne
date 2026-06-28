using System.Globalization;
using System.Net;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;

namespace Throne.Infrastructure.TaskTrackers;

/// <summary>
/// The Kaiten entry in the task-tracker axis catalog (ADR-0045/0046) and its connection surface. The
/// identity half (key + label) makes <c>kaiten</c> appear in the catalog; the connection half adapts
/// the settings axis's provider-neutral calls onto the native HTTP client
/// (<see cref="IKaitenClient"/>) — validating a token and reading the space/board topology — without
/// leaking any Kaiten-internal type past this boundary.
/// </summary>
internal sealed class KaitenTaskTrackerProvider(IKaitenClient client) : ITaskTrackerConnectionProvider
{
    public string TrackerKey => "kaiten";

    public string DisplayName => "Kaiten";

    public async Task<TaskTrackerProbeResult> ProbeAsync(
        TaskTrackerConnectionDescriptor connection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kaiten = ToConnection(connection);
        try
        {
            // Listing spaces is the cheapest authenticated read: a 200 proves the token works and the
            // host is reachable, which is exactly the «can we connect?» question the upsert asks.
            await client.Topology.ListSpacesAsync(kaiten, ct);
            return TaskTrackerProbeResult.Connected();
        }
        catch (KaitenApiException ex) when (IsAuthFailure(ex.StatusCode))
        {
            return TaskTrackerProbeResult.Invalid(ex.Message);
        }
        catch (KaitenApiException ex)
        {
            return TaskTrackerProbeResult.Unreachable(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return TaskTrackerProbeResult.Unreachable(ex.Message);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return TaskTrackerProbeResult.Unreachable($"Request timed out: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<TaskTrackerSpaceTopology>> ListBoardsAsync(
        TaskTrackerConnectionDescriptor connection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kaiten = ToConnection(connection);
        try
        {
            // GET /spaces nests each space's boards inline, so the entire topology is one cheap call —
            // no per-space board fan-out (which would multiply load and trip Kaiten's rate limit).
            // Archived spaces are operator-hidden clutter and are dropped here.
            var spaces = await client.Topology.ListSpacesAsync(kaiten, ct);
            return spaces
                .Where(space => !space.Archived)
                .Select(space => new TaskTrackerSpaceTopology(
                    space.Id.ToString(CultureInfo.InvariantCulture),
                    space.Title,
                    (space.Boards ?? [])
                        .Select(b => new TaskTrackerBoardRef(
                            b.Id.ToString(CultureInfo.InvariantCulture),
                            b.Title))
                        .ToList()))
                .ToList();
        }
        catch (KaitenApiException ex) when (IsAuthFailure(ex.StatusCode))
        {
            // A previously-valid token was revoked/expired: that's a credentials problem the operator
            // must fix by reconnecting, not an upstream outage. Surface it distinctly (409) instead of
            // masquerading it as 502.
            throw TaskTrackerFailures.ConnectionRejected(TrackerKey, ex.Message);
        }
        catch (KaitenApiException ex)
        {
            throw TaskTrackerFailures.UpstreamUnavailable(TrackerKey, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw TaskTrackerFailures.UpstreamUnavailable(TrackerKey, ex.Message);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw TaskTrackerFailures.UpstreamUnavailable(TrackerKey, $"Request timed out: {ex.Message}");
        }
    }

    private static KaitenConnection ToConnection(TaskTrackerConnectionDescriptor connection) =>
        new(connection.BaseUrl, connection.Token);

    private static bool IsAuthFailure(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
