using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Throne.Api.Mcp;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Tests.Mcp;

public class AuditingMcpServerToolTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "InvokeAsync пишет audit success после успешного inner-вызова")]
    public async Task Invoke_records_audit_on_success()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubTool("create_intent", _ => new ValueTask<CallToolResult>(SuccessResult()));
        var tool = NewWrapper(inner, sink);

        var ctx = NewCallContext("create_intent", new Dictionary<string, JsonElement>
        {
            ["text"] = JsonDocument.Parse("\"hello\"").RootElement,
        });

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == "create_intent" &&
                e.Outcome == McpCallOutcome.Success &&
                e.ErrorCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "InvokeAsync пишет audit error с кодом для ApiException и пробрасывает её дальше")]
    public async Task Invoke_records_audit_on_api_exception_and_rethrows()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubTool("get_intent", _ => throw new ApiException(ErrorCodes.IntentNotFound, "no"));
        var tool = NewWrapper(inner, sink);

        var ctx = NewCallContext("get_intent", new Dictionary<string, JsonElement>
        {
            ["intent_id"] = JsonDocument.Parse("\"abc\"").RootElement,
        });

        var act = async () => await tool.InvokeAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<ApiException>();
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == "get_intent" &&
                e.Outcome == McpCallOutcome.Error &&
                e.ErrorCode == ErrorCodes.IntentNotFound &&
                e.IntentId == "abc"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "InvokeAsync проглатывает sink failure и не ломает inner-вызов")]
    public async Task Invoke_swallows_sink_errors()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        sink.WriteAsync(Arg.Any<McpCallLogEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("sink down"));

        var inner = new StubTool("create_intent", _ => new ValueTask<CallToolResult>(SuccessResult()));
        var tool = NewWrapper(inner, sink);

        var ctx = NewCallContext("create_intent", new Dictionary<string, JsonElement>());

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    private static AuditingMcpServerTool NewWrapper(McpServerTool inner, IMcpCallLogSink sink) =>
        new(
            inner,
            sink,
            new FakeTimeProvider(Now),
            NullLogger<AuditingMcpServerTool>.Instance,
            new ServerVersion("test"));

    private static RequestContext<CallToolRequestParams> NewCallContext(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var server = Substitute.For<IMcpServer>();
        return new RequestContext<CallToolRequestParams>(server)
        {
            Params = new CallToolRequestParams
            {
                Name = toolName,
                Arguments = arguments,
            },
        };
    }

    private static CallToolResult SuccessResult() => new()
    {
        Content = [new TextContentBlock { Text = "{\"ok\":true}" }],
        StructuredContent = new JsonObject { ["ok"] = true },
        IsError = false,
    };

    private sealed class StubTool(string name, Func<RequestContext<CallToolRequestParams>, ValueTask<CallToolResult>> handler) : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new() { Name = name, Description = "stub", InputSchema = JsonDocument.Parse("""{ "type": "object" }""").RootElement };

        public override ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken) =>
            handler(request);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
