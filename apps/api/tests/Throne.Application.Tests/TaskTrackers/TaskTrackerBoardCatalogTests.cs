using FluentAssertions;
using Throne.Application.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers;

public class TaskTrackerBoardCatalogTests
{
    private static readonly TaskTrackerConnectionDescriptor Conn = new("https://acme.kaiten.ru", "tok");

    private static readonly IReadOnlyList<TaskTrackerSpaceTopology> Topology =
    [
        new("1", "Engineering", [new TaskTrackerBoardRef("10", "Backlog"), new TaskTrackerBoardRef("11", "In Progress")]),
        new("2", "Design", [new TaskTrackerBoardRef("20", "Ideas")]),
    ];

    [Fact(DisplayName = "Search with no query returns every board, paged")]
    public async Task Search_no_query_returns_all()
    {
        var (catalog, provider) = Build();

        var page = await catalog.SearchAsync("kaiten", provider, Conn, query: null, skip: 0, take: 10, refresh: false, default);

        page.Select(b => b.BoardId).Should().BeEquivalentTo(["10", "11", "20"]);
        provider.Calls.Should().Be(1);
    }

    [Fact(DisplayName = "Search matches board or space title case-insensitively")]
    public async Task Search_filters_by_title()
    {
        var (catalog, provider) = Build();

        var byBoard = await catalog.SearchAsync("kaiten", provider, Conn, "backlog", 0, 10, false, default);
        var bySpace = await catalog.SearchAsync("kaiten", provider, Conn, "design", 0, 10, false, default);

        byBoard.Should().ContainSingle().Which.BoardId.Should().Be("10");
        bySpace.Should().ContainSingle().Which.BoardId.Should().Be("20");
    }

    [Fact(DisplayName = "Search applies skip/take")]
    public async Task Search_pages()
    {
        var (catalog, provider) = Build();

        var page = await catalog.SearchAsync("kaiten", provider, Conn, null, skip: 1, take: 1, refresh: false, default);

        page.Should().ContainSingle().Which.BoardId.Should().Be("11");
    }

    [Fact(DisplayName = "Topology is loaded once and reused within the TTL")]
    public async Task Caches_within_ttl()
    {
        var (catalog, provider) = Build();

        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, false, default);
        await catalog.SearchAsync("kaiten", provider, Conn, "ideas", 0, 10, false, default);

        provider.Calls.Should().Be(1);
    }

    [Fact(DisplayName = "Cache expires after the TTL and reloads")]
    public async Task Reloads_after_ttl()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (catalog, provider) = Build(clock);

        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, false, default);
        clock.Advance(TimeSpan.FromMinutes(6));
        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, false, default);

        provider.Calls.Should().Be(2);
    }

    [Fact(DisplayName = "refresh=true drops the cache and reloads even within the TTL")]
    public async Task Refresh_forces_reload()
    {
        var (catalog, provider) = Build();

        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, false, default);
        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, refresh: true, default);

        provider.Calls.Should().Be(2);
    }

    [Fact(DisplayName = "Invalidate drops the cache so the next search reloads")]
    public async Task Invalidate_drops_cache()
    {
        var (catalog, provider) = Build();

        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, false, default);
        catalog.Invalidate("kaiten");
        await catalog.SearchAsync("kaiten", provider, Conn, null, 0, 10, false, default);

        provider.Calls.Should().Be(2);
    }

    private static (TaskTrackerBoardCatalog, CountingProvider) Build(TimeProvider? clock = null)
    {
        var provider = new CountingProvider(Topology);
        return (new TaskTrackerBoardCatalog(clock ?? TimeProvider.System), provider);
    }

    private sealed class CountingProvider(IReadOnlyList<TaskTrackerSpaceTopology> topology)
        : ITaskTrackerConnectionProvider
    {
        public int Calls { get; private set; }

        public string TrackerKey => "kaiten";

        public string DisplayName => "Kaiten";

        public Task<TaskTrackerProbeResult> ProbeAsync(TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
            Task.FromResult(TaskTrackerProbeResult.Connected());

        public Task<IReadOnlyList<TaskTrackerSpaceTopology>> ListBoardsAsync(
            TaskTrackerConnectionDescriptor connection, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(topology);
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
