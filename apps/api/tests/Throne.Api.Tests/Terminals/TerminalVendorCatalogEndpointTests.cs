using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Terminals;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TerminalVendorCatalogEndpointTests(SqliteFixture sqlite) : IAsyncLifetime
{
    private static readonly Uri Endpoint = new("/api/v1/terminal/vendors", UriKind.Relative);

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private SqliteTestDatabase _database = null!;

    public Task InitializeAsync()
    {
        _database = sqlite.CreateDatabase();
        _factory = SqliteTestHost.Create(_database);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact(DisplayName = "GET /terminal/vendors: efforts отдаются строками, не int-кодами enum'а")]
    public async Task Efforts_are_serialized_as_strings()
    {
        var response = await _client.GetAsync(Endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var claude = doc.RootElement.GetProperty("vendors")
            .EnumerateArray()
            .Single(v => v.GetProperty("vendor").GetString() == "claude");

        var efforts = claude.GetProperty("efforts");
        efforts.EnumerateArray()
            .Select(e => e.ValueKind)
            .Should().AllBeEquivalentTo(JsonValueKind.String);
        efforts.EnumerateArray()
            .Select(e => e.GetString())
            .Should().Equal("low", "medium", "high", "xhigh");
    }
}
