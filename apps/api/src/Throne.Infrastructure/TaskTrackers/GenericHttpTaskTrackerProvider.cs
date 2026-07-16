using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.GenericHttp;

namespace Throne.Infrastructure.TaskTrackers;

internal sealed class GenericHttpTaskTrackerProvider(GenericHttpClient client) : ITaskTrackerConnectionProvider
{
    public string TrackerKey => "custom-http";

    public string DisplayName => "Custom HTTP";

    public async Task<TaskTrackerProbeResult> ProbeAsync(
        TaskTrackerConnectionDescriptor connection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            await client.ProbeAsync(ToConnection(connection), ct);
            return TaskTrackerProbeResult.Connected();
        }
        catch (GenericHttpApiException ex)
        {
            return TaskTrackerProbeResult.FromHealth(GenericHttpFailureMap.Classify(ex.StatusCode), ex.Message);
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
        try
        {
            var boards = await client.ListBoardsAsync(ToConnection(connection), ct);
            return [GenericHttpProjection.ToTopology(boards)];
        }
        catch (GenericHttpApiException ex)
        {
            throw GenericHttpFailureMap.BoardReadFailure(
                TrackerKey, GenericHttpFailureMap.Classify(ex.StatusCode), ex.Message);
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
        CancellationToken ct) =>
        await PullCardsAsync(connection, boardId, c => client.ListCardsAsync(c, boardId, ct), ct);

    public async Task<IReadOnlyList<TaskTrackerCard>> SearchCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        string? query,
        int limit,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return await PullCardsAsync(
            connection,
            boardId,
            c => client.SearchCardsAsync(c, boardId, query, limit, ct),
            ct);
    }

    public async Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection,
        string cardId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        try
        {
            var card = await client.GetCardAsync(ToConnection(connection), cardId, ct);
            return GenericHttpProjection.ToCard(card);
        }
        catch (GenericHttpApiException ex) when (GenericHttpFailureMap.IsGone(ex.StatusCode))
        {
            return null;
        }
        catch (GenericHttpApiException ex)
        {
            throw new TaskTrackerConnectionException(GenericHttpFailureMap.Classify(ex.StatusCode), ex.Message);
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

    public string? BuildCardWebUrl(TaskTrackerConnectionDescriptor connection, string cardId) => null;

    private static async Task<IReadOnlyList<TaskTrackerCard>> PullCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        Func<GenericHttpConnection, Task<IReadOnlyList<GenericHttpCardDto>>> pull,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        try
        {
            return (await pull(ToConnection(connection)))
                .Where(card => !card.Archived)
                .Select(GenericHttpProjection.ToCard)
                .ToList();
        }
        catch (GenericHttpApiException ex)
        {
            throw new TaskTrackerConnectionException(GenericHttpFailureMap.Classify(ex.StatusCode), ex.Message);
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

    private static GenericHttpConnection ToConnection(TaskTrackerConnectionDescriptor connection) =>
        new(connection.BaseUrl, connection.Token);
}
