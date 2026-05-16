using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Throne.Mcp.Stdio.Tests;

public sealed class StdioProxyBootstrapTests
{
    [Fact(DisplayName = "AddStdioProxy форвардит initial ServerInstructions из ResilientUpstreamClient в McpServerOptions (ADR-0014)")]
    public async Task Forwards_initial_server_instructions()
    {
        const string router = "This is Throne. ...";

        var upstream = await CreateUpstreamWithoutConnectAsync(router);

        var options = ResolveServerOptions(upstream);

        options.ServerInstructions.Should().Be(router);
    }

    [Fact(DisplayName = "AddStdioProxy при upstream.InitialServerInstructions=null не подставляет фолбэк")]
    public async Task Forwards_null_when_upstream_has_no_instructions()
    {
        var upstream = await CreateUpstreamWithoutConnectAsync(null);

        var options = ResolveServerOptions(upstream);

        options.ServerInstructions.Should().BeNull();
    }

    [Theory(DisplayName = "RetryPolicy повторяет только read-only или idempotent тул")]
    [InlineData(true, null, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(null, null, false)]
    public void Retry_policy_allows_only_safe_tools(bool? readOnly, bool? idempotent, bool expected)
    {
        var tools = new[]
        {
            new Tool
            {
                Name = "tool",
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = readOnly,
                    IdempotentHint = idempotent,
                },
            },
        };

        UpstreamToolRetryPolicy.CanRetry("tool", tools).Should().Be(expected);
        UpstreamToolRetryPolicy.CanRetry("missing", tools).Should().BeFalse();
    }

    private static McpServerOptions ResolveServerOptions(ResilientUpstreamClient upstream)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStdioProxy(upstream);
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
    }

    /// <summary>
    /// Сборка <see cref="ResilientUpstreamClient"/> без живого upstream'а: подменяем
    /// connectAsync на reflection-фабрику, которая через приватные поля выставляет
    /// initial ServerInstructions без реального MCP-handshake.
    /// </summary>
    private static Task<ResilientUpstreamClient> CreateUpstreamWithoutConnectAsync(string? initialInstructions)
    {
        var upstream = new ResilientUpstreamClient(
            connectAsync: _ => throw new InvalidOperationException("connect should not be called in this test"),
            log: NullLogger<ResilientUpstreamClient>.Instance);

        SetField(upstream.Connection.InstructionsState, "_initial", initialInstructions);
        SetField(upstream.Connection.InstructionsState, "_locked", true);
        return Task.FromResult(upstream);

        static void SetField(object target, string name, object? value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(target, value);
        }
    }
}
