using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using NSubstitute;

namespace Throne.Mcp.Stdio.Tests;

public sealed class StdioProxyBootstrapTests
{
    [Fact(DisplayName = "AddStdioProxy форвардит upstream.ServerInstructions в McpServerOptions (ADR-0014)")]
    public void Forwards_upstream_server_instructions()
    {
        const string router = "This is Throne. ...";

        var upstream = Substitute.For<IMcpClient>();
        upstream.ServerInstructions.Returns(router);

        var options = ResolveServerOptions(upstream, []);

        options.ServerInstructions.Should().Be(router);
    }

    [Fact(DisplayName = "AddStdioProxy при upstream.ServerInstructions=null не подставляет фолбэк")]
    public void Forwards_null_when_upstream_has_no_instructions()
    {
        var upstream = Substitute.For<IMcpClient>();
        upstream.ServerInstructions.Returns((string?)null);

        var options = ResolveServerOptions(upstream, []);

        options.ServerInstructions.Should().BeNull();
    }

    private static McpServerOptions ResolveServerOptions(
        IMcpClient upstream,
        IEnumerable<McpClientTool> tools)
    {
        var services = new ServiceCollection();
        services.AddStdioProxy(upstream, tools);
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
    }
}
