using System.Globalization;
using System.Net;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

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
        catch (KaitenApiException ex)
        {
            return TaskTrackerProbeResult.FromHealth(KaitenTaskTrackerFailureMap.Classify(ex.StatusCode), ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return TaskTrackerProbeResult.Offline(ex.Message);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return TaskTrackerProbeResult.Offline($"Request timed out: {ex.Message}");
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
        catch (KaitenApiException ex)
        {
            // A previously-valid connection can fail three ways on board read: a revoked token
            // (auth → 409 reconnect), a tariff wall (blocked → 402) or an outage (offline → 502).
            // Distinguishing them here is what lets the settings card tell the operator what to do
            // instead of masquerading everything as a generic 502.
            throw KaitenTaskTrackerFailureMap.BoardReadFailure(
                TrackerKey, KaitenTaskTrackerFailureMap.Classify(ex.StatusCode), ex.Message);
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

    public async Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        var kaiten = ToConnection(connection);
        var board = KaitenCardProjection.ParseId(boardId);
        try
        {
            // condition=1 asks Kaiten for live cards only; the post-filter is belt-and-suspenders so an
            // archived row can never leak into the browser even if the server ignores the filter.
            var query = new KaitenCardQuery(BoardId: board, Condition: KaitenCardConditions.Live);
            var cards = await client.Cards.ListAllCardsAsync(kaiten, query, ct);
            var columnTitles = await LoadColumnTitlesAsync(kaiten, board, ct);
            return cards
                .Where(c => c.Condition == KaitenCardConditions.Live)
                .Select(c => KaitenCardProjection.ToCard(c, columnTitles))
                .ToList();
        }
        catch (KaitenApiException ex)
        {
            throw new TaskTrackerConnectionException(KaitenTaskTrackerFailureMap.Classify(ex.StatusCode), ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Offline, ex.Message);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TaskTrackerConnectionException(
                TaskTrackerConnectionHealth.Offline, $"Request timed out: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<TaskTrackerCard>> SearchCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        string? query,
        int limit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var kaiten = ToConnection(connection);
        var board = KaitenCardProjection.ParseId(boardId);
        var trimmed = query?.Trim();
        var hasQuery = !string.IsNullOrEmpty(trimmed);
        try
        {
            // With an empty query we want the recently touched cards, so ask Kaiten to sort by
            // updated_at desc; a non-empty query hands the text filter to Kaiten and relevance ordering
            // stays server-side. Single page — the picker is latency-sensitive, not exhaustive.
            var kaitenQuery = new KaitenCardQuery(
                BoardId: board,
                Condition: KaitenCardConditions.Live,
                Limit: limit,
                Query: hasQuery ? trimmed : null,
                OrderBy: hasQuery ? null : "updated",
                OrderDirection: hasQuery ? null : "desc");
            var cards = await client.Cards.ListCardsAsync(kaiten, kaitenQuery, ct);
            var columnTitles = await LoadColumnTitlesAsync(kaiten, board, ct);
            var projected = cards
                .Where(c => c.Condition == KaitenCardConditions.Live)
                .Select(c => KaitenCardProjection.ToCard(c, columnTitles))
                .ToList();
            // Sort belt-and-suspenders for the empty-query case: even if Kaiten ignores order_by, the
            // caller still sees «recent first» within the returned page.
            if (!hasQuery)
            {
                projected.Sort(static (a, b) =>
                    Nullable.Compare(b.UpdatedAt, a.UpdatedAt));
            }
            return projected;
        }
        catch (KaitenApiException ex)
        {
            throw new TaskTrackerConnectionException(KaitenTaskTrackerFailureMap.Classify(ex.StatusCode), ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Offline, ex.Message);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TaskTrackerConnectionException(
                TaskTrackerConnectionHealth.Offline, $"Request timed out: {ex.Message}");
        }
    }

    public async Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection,
        string cardId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var kaiten = ToConnection(connection);
        try
        {
            var card = await client.Cards.GetCardAsync(kaiten, KaitenCardProjection.ParseId(cardId), ct);
            var columnTitles = await LoadColumnTitlesAsync(kaiten, card.BoardId, ct);
            return KaitenCardProjection.ToCard(card, columnTitles);
        }
        catch (KaitenApiException ex) when (KaitenTaskTrackerFailureMap.IsGone(ex.StatusCode))
        {
            // Only a genuine 404 is «gone» — the card was deleted. A 403 is NOT gone: it means the
            // token lost access (auth), and treating it as gone would wrongly bury a revoked-token
            // signal under a per-card «vanished», so it falls through to the classified throw below.
            return null;
        }
        catch (KaitenApiException ex)
        {
            throw new TaskTrackerConnectionException(KaitenTaskTrackerFailureMap.Classify(ex.StatusCode), ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Offline, ex.Message);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TaskTrackerConnectionException(
                TaskTrackerConnectionHealth.Offline, $"Request timed out: {ex.Message}");
        }
    }

    public string? BuildCardWebUrl(TaskTrackerConnectionDescriptor connection, string cardId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (string.IsNullOrWhiteSpace(connection.BaseUrl) || string.IsNullOrWhiteSpace(cardId))
        {
            return null;
        }

        // Short form Kaiten redirects to the human-readable /space/{s}/boards/card/{prefix}-{n}. Only the
        // numeric card id is part of the attachment coordinate, so we do not fabricate space/prefix here.
        if (!long.TryParse(cardId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return null;
        }

        var trimmed = connection.BaseUrl.TrimEnd('/');
        return $"{trimmed}/{cardId}";
    }

    private async Task<IReadOnlyDictionary<long, string>> LoadColumnTitlesAsync(
        KaitenConnection kaiten, long boardId, CancellationToken ct)
    {
        var columns = await client.Topology.ListColumnsAsync(kaiten, boardId, ct);
        return columns.ToDictionary(c => c.Id, c => c.Title);
    }

    private static KaitenConnection ToConnection(TaskTrackerConnectionDescriptor connection) =>
        new(connection.BaseUrl, connection.Token);
}
