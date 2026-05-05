using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Throne.Api.Mcp;
using Throne.Application.Auth;
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
                e.ErrorCode == null &&
                e.UserId == CurrentUserIds.LocalDev),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "InvokeAsync пишет user_id из ICurrentUserAccessor")]
    public async Task Invoke_records_user_id_from_accessor()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubTool("create_intent", _ => new ValueTask<CallToolResult>(SuccessResult()));
        var tool = NewWrapper(inner, sink, new StubCurrentUser("user-42"));

        await tool.InvokeAsync(NewCallContext("create_intent", new Dictionary<string, JsonElement>()), CancellationToken.None);

        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e => e.UserId == "user-42"),
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

    [Fact(DisplayName = "InvokeAsync пишет InstructionBundleUse projection для get_instruction_bundle")]
    public async Task Invoke_summarizes_instruction_bundle_use()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubTool("get_instruction_bundle", _ => new ValueTask<CallToolResult>(InstructionBundleResult()));
        var tool = NewWrapper(inner, sink);

        var ctx = NewCallContext("get_instruction_bundle", new Dictionary<string, JsonElement>
        {
            ["intent_id"] = JsonDocument.Parse("\"intent_123\"").RootElement,
            ["mode"] = JsonDocument.Parse("\"work\"").RootElement,
        });

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == "get_instruction_bundle" &&
                e.IntentId == "intent_123" &&
                e.ModeHint == "work" &&
                HasInstructionRefs(e.ResultSummary)),
            Arg.Any<CancellationToken>());
    }

    private static AuditingMcpServerTool NewWrapper(
        McpServerTool inner,
        IMcpCallLogSink sink,
        ICurrentUserAccessor? currentUser = null) =>
        new(
            inner,
            sink,
            currentUser ?? new StubCurrentUser(CurrentUserIds.LocalDev),
            new FakeTimeProvider(Now),
            NullLogger<AuditingMcpServerTool>.Instance,
            new ServerVersion("test"));

    private sealed class StubCurrentUser(string userId) : ICurrentUserAccessor
    {
        public string UserId { get; } = userId;
    }

    private static RequestContext<CallToolRequestParams> NewCallContext(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var server = Substitute.For<IMcpServer>();
        server.SessionId.Returns("session-1");
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

    private static CallToolResult InstructionBundleResult() => new()
    {
        Content = [new TextContentBlock { Text = "{\"ok\":true}" }],
        StructuredContent = new JsonObject
        {
            ["intent_id"] = "intent_123",
            ["mode"] = "work",
            ["instructions"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "common",
                    ["instruction_id"] = "instr_common_1",
                    ["current_version"] = 2,
                    ["text"] = "full common text",
                },
                new JsonObject
                {
                    ["kind"] = "work",
                    ["instruction_id"] = "instr_light_1",
                    ["current_version"] = 4,
                    ["text"] = "full light text",
                },
            },
            ["missing_kinds"] = new JsonArray(),
        },
        IsError = false,
    };

    private static bool HasInstructionRefs(IReadOnlyDictionary<string, object?>? summary)
    {
        if (summary is null ||
            !summary.TryGetValue("instructions", out var instructions) ||
            instructions is not List<Dictionary<string, object?>> refs)
        {
            return false;
        }

        refs.Should().HaveCount(2);
        refs[0].Should().ContainKey("kind").WhoseValue.Should().Be("common");
        refs[0].Should().ContainKey("instruction_id").WhoseValue.Should().Be("instr_common_1");
        refs[0].Should().ContainKey("version").WhoseValue.Should().Be(2);
        refs[0].Should().NotContainKey("text");
        refs[1].Should().ContainKey("kind").WhoseValue.Should().Be("work");
        refs[1].Should().ContainKey("version").WhoseValue.Should().Be(4);
        return true;
    }

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
