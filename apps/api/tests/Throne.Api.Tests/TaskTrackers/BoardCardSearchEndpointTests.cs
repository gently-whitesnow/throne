using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.TaskTrackers;

namespace Throne.Api.Tests.TaskTrackers;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class BoardCardSearchEndpointTests(SqliteFixture sqlite) : IAsyncLifetime, IDisposable
{
    private CardAttachmentsApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new CardAttachmentsApiFixture(sqlite);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "GET search → 200 с картами (пустой query = топ по updated_at)")]
    public async Task Search_ok_empty_query()
    {
        await _fixture.SeedConnectionAsync();
        string? seenQuery = "sentinel";
        var seenLimit = -1;
        _fixture.Provider.OnSearchCards = (_, q, l) =>
        {
            seenQuery = q;
            seenLimit = l;
            return Task.FromResult<IReadOnlyList<TaskTrackerCard>>(
                [StubCardTrackerProvider.Card("42", "Recent")]);
        };

        var response = await _fixture.Client.GetAsync(SearchUri("kaiten", "10", query: null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("cards")[0].GetProperty("card_id").GetString().Should().Be("42");
        seenQuery.Should().BeNull();
        seenLimit.Should().Be(10);
    }

    [Fact(DisplayName = "GET search → query прокидывается, limit клампится")]
    public async Task Search_forwards_query_and_clamps_limit()
    {
        await _fixture.SeedConnectionAsync();
        string? seenQuery = null;
        var seenLimit = 0;
        _fixture.Provider.OnSearchCards = (_, q, l) =>
        {
            seenQuery = q;
            seenLimit = l;
            return Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);
        };

        var response = await _fixture.Client.GetAsync(SearchUri("kaiten", "10", "bug", 999));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        seenQuery.Should().Be("bug");
        seenLimit.Should().Be(25);
    }

    [Fact(DisplayName = "GET search → 409 без коннекта")]
    public async Task Search_not_connected_409()
    {
        var response = await _fixture.Client.GetAsync(SearchUri("kaiten", "10", null));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "GET search → 502 когда Offline")]
    public async Task Search_offline_502()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnSearchCards = (_, _, _) =>
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Offline, "down");

        var response = await _fixture.Client.GetAsync(SearchUri("kaiten", "10", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    private static Uri SearchUri(string tracker, string board, string? query, int? limit = null)
    {
        var parts = new List<string>(2);
        if (query is not null)
        {
            parts.Add($"query={Uri.EscapeDataString(query)}");
        }
        if (limit is not null)
        {
            parts.Add($"limit={limit}");
        }
        var suffix = parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
        return new($"/api/v1/task-trackers/{tracker}/boards/{board}/cards/search{suffix}", UriKind.Relative);
    }
}
