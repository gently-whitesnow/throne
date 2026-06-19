using System.Text.Json.Nodes;
using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class ClaudeMcpDocumentTests
{
    [Fact(DisplayName = "Claude MCP: создаёт project .mcp.json с http throne server")]
    public void Seeds_into_empty_document()
    {
        var updated = ClaudeMcpDocument.WithThroneServer(null, "http://localhost:5008/");

        Server(updated!, "throne")!["type"]!.GetValue<string>().Should().Be("http");
        Server(updated!, "throne")!["url"]!.GetValue<string>().Should().Be("http://localhost:5008/mcp");
        ClaudeMcpDocument.WithThroneServer(updated, "http://localhost:5008").Should().BeNull();
    }

    [Fact(DisplayName = "Claude MCP: сохраняет чужие mcpServers и обновляет только throne")]
    public void Preserves_sibling_servers()
    {
        var existing = """
        {
          "mcpServers": {
            "github": { "type": "stdio", "command": "github-mcp" },
            "throne": { "type": "http", "url": "http://old/mcp" }
          }
        }
        """;

        var updated = ClaudeMcpDocument.WithThroneServer(existing, "http://localhost:5008");

        Server(updated!, "github")!["command"]!.GetValue<string>().Should().Be("github-mcp");
        Server(updated!, "throne")!["url"]!.GetValue<string>().Should().Be("http://localhost:5008/mcp");
    }

    [Theory(DisplayName = "Claude MCP: не затирает нечитаемый или несовместимый файл")]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "mcpServers": "oops" }""")]
    public void Refuses_to_clobber(string existing)
    {
        ClaudeMcpDocument.WithThroneServer(existing, "http://localhost:5008").Should().BeNull();
    }

    [Fact(DisplayName = "Claude settings: добавляет throne в enabledMcpjsonServers без дублей")]
    public void Enables_throne_mcpjson_server()
    {
        var existing = """{ "enabledMcpjsonServers": ["github"], "theme": "dark" }""";

        var updated = ClaudeProjectSettingsDocument.WithThroneMcpEnabled(existing);

        var root = JsonNode.Parse(updated!)!.AsObject();
        root["theme"]!.GetValue<string>().Should().Be("dark");
        root["enabledMcpjsonServers"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Equal("github", "throne");
        ClaudeProjectSettingsDocument.WithThroneMcpEnabled(updated).Should().BeNull();
    }

    private static JsonNode? Server(string json, string name) =>
        JsonNode.Parse(json)!["mcpServers"]![name];
}
