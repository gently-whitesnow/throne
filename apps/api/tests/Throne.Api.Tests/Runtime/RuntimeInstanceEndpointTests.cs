using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Runtime;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Runtime;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class RuntimeInstanceEndpointTests(SqliteFixture sqlite)
{
    [Fact(DisplayName = "Runtime instance по умолчанию не ephemeral")]
    public async Task Default_runtime_instance_is_not_ephemeral()
    {
        await using var database = sqlite.CreateDatabase();
        await using var factory = SqliteTestHost.Create(database);
        using var client = factory.CreateClient();

        var dto = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/api/v1/runtime/instance", UriKind.Relative));

        dto.GetProperty("ephemeral").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Runtime instance под intents/<id> считается ephemeral")]
    public async Task Intent_workspace_runtime_instance_is_ephemeral()
    {
        await using var database = sqlite.CreateDatabase();
        var workspace = Path.Combine(
            Path.GetTempPath(),
            "throne-workspaces",
            "intents",
            "intent-1",
            ".throne-test",
            "workspaces");
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => SqliteTestHost.Configure(
                builder,
                database.DataSource,
                new Dictionary<string, string?>
                {
                    ["Throne:Workspace:Root"] = workspace,
                }));
        using var client = factory.CreateClient();

        var dto = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/api/v1/runtime/instance", UriKind.Relative));

        dto.GetProperty("ephemeral").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Path detector требует сегмент intents с дочерним id")]
    public void Path_detector_requires_intents_segment_with_child()
    {
        RuntimeInstanceDetector.LooksInsideIntentWorkspace("/tmp/not-intents-here/.throne")
            .Should().BeFalse();
        RuntimeInstanceDetector.LooksInsideIntentWorkspace("/tmp/ws/intents")
            .Should().BeFalse();
        RuntimeInstanceDetector.LooksInsideIntentWorkspace("/tmp/ws/intents/abc/.throne-test")
            .Should().BeTrue();
    }
}
