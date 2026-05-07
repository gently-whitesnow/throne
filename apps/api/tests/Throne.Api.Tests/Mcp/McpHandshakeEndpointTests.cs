using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Instructions;

namespace Throne.Api.Tests.Mcp;

[Collection(nameof(MongoIntegrationFixture))]
public sealed class McpHandshakeEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var dbName = $"throne_mcp_handshake_{Guid.NewGuid():N}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateScopes = false;
                o.ValidateOnBuild = false;
            });
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mongo:ConnectionString"] = mongo.ConnectionString,
                    ["Mongo:Database"] = dbName,
                });
            });
        });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "MCP initialize: InitializeResult.instructions = ThroneServerInstructions.MiniRouter (ADR-0014)")]
    public async Task Initialize_returns_mini_router_instructions()
    {
        var endpoint = new Uri(_client.BaseAddress!, "/mcp");
        var transport = new SseClientTransport(
            new SseClientTransportOptions { Endpoint = endpoint, Name = "throne-test" },
            _client,
            loggerFactory: null,
            ownsHttpClient: false);

        await using var mcp = await McpClientFactory.CreateAsync(
            transport,
            clientOptions: null,
            loggerFactory: null);

        mcp.ServerInstructions.Should().Be(ThroneServerInstructions.MiniRouter);
    }
}
