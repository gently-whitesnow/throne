using System.Collections.Concurrent;

namespace Throne.Application.TaskTrackers;

/// <summary>
/// In-memory board catalog for the task-tracker axis. Caches the flattened board topology per tracker so
/// a board search filters in memory instead of hitting the tracker on every keystroke. The topology is
/// loaded lazily — once per tracker, guarded by a per-tracker semaphore with a double-check — and expires
/// after <see cref="Ttl"/>; an explicit refresh or a connection change invalidates it eagerly. Kept
/// provider-neutral: the caller resolves the provider/connection, the catalog only reads and caches.
/// </summary>
public sealed class TaskTrackerBoardCatalog(TimeProvider clock)
{
    /// <summary>Stale-after window. A search past this re-reads the topology; a fresh hit serves from cache.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _byTracker = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadLocks = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<TaskTrackerBoardMatch>> SearchAsync(
        string tracker,
        ITaskTrackerConnectionProvider provider,
        TaskTrackerConnectionDescriptor connection,
        string? query,
        int skip,
        int take,
        bool refresh,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(connection);

        if (refresh)
        {
            Invalidate(tracker);
        }

        var boards = await EnsureLoadedAsync(tracker, provider, connection, ct);

        IEnumerable<TaskTrackerBoardMatch> matches = boards;
        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = boards.Where(b =>
                b.BoardTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                b.SpaceTitle.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return matches
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(0, take))
            .ToList();
    }

    /// <summary>Drop the cached topology for a tracker (connection changed / removed, or explicit refresh).</summary>
    public void Invalidate(string tracker) => _byTracker.TryRemove(tracker, out _);

    private async Task<IReadOnlyList<TaskTrackerBoardMatch>> EnsureLoadedAsync(
        string tracker,
        ITaskTrackerConnectionProvider provider,
        TaskTrackerConnectionDescriptor connection,
        CancellationToken ct)
    {
        if (TryGetFresh(tracker, out var cached))
        {
            return cached;
        }

        var gate = _loadLocks.GetOrAdd(tracker, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (TryGetFresh(tracker, out cached))
            {
                return cached;
            }

            var topology = await provider.ListBoardsAsync(connection, ct);
            var boards = Flatten(topology);
            _byTracker[tracker] = new CacheEntry(boards, clock.GetUtcNow());
            return boards;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetFresh(string tracker, out IReadOnlyList<TaskTrackerBoardMatch> boards)
    {
        if (_byTracker.TryGetValue(tracker, out var entry) && clock.GetUtcNow() - entry.LoadedAt < Ttl)
        {
            boards = entry.Boards;
            return true;
        }

        boards = [];
        return false;
    }

    private static List<TaskTrackerBoardMatch> Flatten(IReadOnlyList<TaskTrackerSpaceTopology> topology)
    {
        var boards = new List<TaskTrackerBoardMatch>();
        foreach (var space in topology)
        {
            foreach (var board in space.Boards)
            {
                boards.Add(new TaskTrackerBoardMatch(board.BoardId, board.BoardTitle, space.SpaceId, space.SpaceTitle));
            }
        }

        return boards;
    }

    private sealed record CacheEntry(IReadOnlyList<TaskTrackerBoardMatch> Boards, DateTimeOffset LoadedAt);
}
