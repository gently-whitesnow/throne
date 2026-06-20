using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class CodexMcpDocumentTests
{
    [Fact(DisplayName = "Codex MCP: создаёт workspace .codex/config.toml со streamable-http url")]
    public void Seeds_into_empty_document()
    {
        var updated = CodexMcpDocument.WithThroneServer(null, "http://localhost:5008/");

        updated.Should().Contain("[mcp_servers.throne]");
        updated.Should().Contain("url = \"http://localhost:5008/mcp\"");
        CodexMcpDocument.WithThroneServer(updated, "http://localhost:5008").Should().BeNull();
    }

    [Fact(DisplayName = "Codex MCP: добавляет throne, сохраняя прочие настройки и MCP servers")]
    public void Adds_entry_preserving_other_keys()
    {
        var existing = """
        model = "gpt-5"

        [mcp_servers.github]
        command = "github-mcp"
        """;

        var updated = CodexMcpDocument.WithThroneServer(existing, "http://localhost:5008");

        updated.Should().Contain("model = \"gpt-5\"");
        updated.Should().Contain("[mcp_servers.github]");
        updated.Should().Contain("command = \"github-mcp\"");
        updated.Should().Contain("[mcp_servers.throne]");
        updated.Should().Contain("url = \"http://localhost:5008/mcp\"");
    }

    [Fact(DisplayName = "Codex MCP: переводит старую throne-запись на url и убирает stdio ключи")]
    public void Updates_existing_throne_entry()
    {
        var existing = """
        [mcp_servers.throne]
        command = "old-throne"
        args = ["--stdio"]
        env = { TOKEN = "x" }
        """;

        var updated = CodexMcpDocument.WithThroneServer(existing, "http://localhost:5008");

        updated.Should().Contain("url = \"http://localhost:5008/mcp\"");
        updated.Should().NotContain("command = ");
        updated.Should().NotContain("args = ");
        updated.Should().NotContain("env = ");
        CodexMcpDocument.WithThroneServer(updated, "http://localhost:5008").Should().BeNull();
    }

    [Fact(DisplayName = "Codex MCP: распознаёт quoted throne table как ту же запись")]
    public void Updates_quoted_throne_entry()
    {
        var existing = """
        [mcp_servers."throne"]
        url = "http://old/mcp"
        """;

        var updated = CodexMcpDocument.WithThroneServer(existing, "http://localhost:5008");

        updated.Should().Contain("url = \"http://localhost:5008/mcp\"");
        updated.Should().NotContain("[mcp_servers.throne]\nurl = \"http://localhost:5008/mcp\"");
    }

    [Theory(DisplayName = "Codex MCP: не затирает TOML, который мог бы испортить")]
    [InlineData("= no key here")]
    [InlineData("mcp_servers = \"oops\"")]
    public void Refuses_to_clobber(string existing)
    {
        CodexMcpDocument.WithThroneServer(existing, "http://localhost:5008").Should().BeNull();
    }
}
