using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Throne.Api.Mcp;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Api.Tests.Mcp;

public class AuditingMcpServerPromptTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetAsync пишет audit success с tool_name = prompts/get:<name>")]
    public async Task Get_records_audit_on_success()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubPrompt("tinterview", _ => new ValueTask<GetPromptResult>(SuccessResult("hello")));
        var prompt = NewWrapper(inner, sink);

        var ctx = NewContext("tinterview", new Dictionary<string, JsonElement>
        {
            ["intent_id"] = JsonDocument.Parse("\"intent_42\"").RootElement,
        });

        var result = await prompt.GetAsync(ctx, CancellationToken.None);

        result.Messages.Should().HaveCount(1);
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == "prompts/get:tinterview" &&
                e.IntentId == "intent_42" &&
                e.ModeHint == "interview" &&
                e.Outcome == McpCallOutcome.Success &&
                e.ErrorCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GetAsync пишет mode_hint по статическому маппингу для всех 4 команд")]
    public async Task Get_writes_mode_hint_per_command()
    {
        var cases = new[]
        {
            ("tinterview", "interview"),
            ("twork", "light_work"),
            ("tnew", "new_project"),
            ("treview", "light_work"),
        };

        foreach (var (name, expectedMode) in cases)
        {
            var sink = Substitute.For<IMcpCallLogSink>();
            var inner = new StubPrompt(name, _ => new ValueTask<GetPromptResult>(SuccessResult("ok")));
            var prompt = NewWrapper(inner, sink);

            await prompt.GetAsync(NewContext(name, new Dictionary<string, JsonElement>()), CancellationToken.None);

            await sink.Received(1).WriteAsync(
                Arg.Is<McpCallLogEntry>(e =>
                    e.ToolName == $"prompts/get:{name}" &&
                    e.ModeHint == expectedMode),
                Arg.Any<CancellationToken>());
        }
    }

    [Fact(DisplayName = "GetAsync пишет error и пробрасывает ApiException")]
    public async Task Get_records_error_on_api_exception()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubPrompt("tinterview", _ => throw new ApiException("intent.not_found", "no"));
        var prompt = NewWrapper(inner, sink);

        var act = async () => await prompt.GetAsync(NewContext("tinterview", new Dictionary<string, JsonElement>()), CancellationToken.None);

        await act.Should().ThrowAsync<ApiException>();
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == "prompts/get:tinterview" &&
                e.Outcome == McpCallOutcome.Error &&
                e.ErrorCode == "intent.not_found"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GetAsync проглатывает sink failure и не ломает inner-вызов")]
    public async Task Get_swallows_sink_errors()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        sink.WriteAsync(Arg.Any<McpCallLogEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("sink down"));

        var inner = new StubPrompt("twork", _ => new ValueTask<GetPromptResult>(SuccessResult("ok")));
        var prompt = NewWrapper(inner, sink);

        var result = await prompt.GetAsync(NewContext("twork", new Dictionary<string, JsonElement>()), CancellationToken.None);

        result.Messages.Should().HaveCount(1);
    }

    [Fact(DisplayName = "result_summary пишет messages_count и user_chars без полного текста")]
    public async Task Get_summary_keeps_only_lengths()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var inner = new StubPrompt("tnew", _ => new ValueTask<GetPromptResult>(SuccessResult("hello world")));
        var prompt = NewWrapper(inner, sink);

        await prompt.GetAsync(NewContext("tnew", new Dictionary<string, JsonElement>()), CancellationToken.None);

        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ResultSummary != null &&
                (int)e.ResultSummary["messages_count"]! == 1 &&
                (int)e.ResultSummary["user_chars"]! == "hello world".Length &&
                !e.ResultSummary.ContainsKey("text")),
            Arg.Any<CancellationToken>());
    }

    private static AuditingMcpServerPrompt NewWrapper(McpServerPrompt inner, IMcpCallLogSink sink) =>
        new(
            inner,
            sink,
            new FakeTimeProvider(Now),
            NullLogger<AuditingMcpServerPrompt>.Instance,
            new ServerVersion("test"));

    private static RequestContext<GetPromptRequestParams> NewContext(
        string promptName,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var server = Substitute.For<IMcpServer>();
        server.SessionId.Returns("session-1");
        return new RequestContext<GetPromptRequestParams>(server)
        {
            Params = new GetPromptRequestParams
            {
                Name = promptName,
                Arguments = arguments,
            },
        };
    }

    private static GetPromptResult SuccessResult(string text) => new()
    {
        Description = "stub",
        Messages =
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock { Text = text },
            },
        ],
    };

    private sealed class StubPrompt(string name, Func<RequestContext<GetPromptRequestParams>, ValueTask<GetPromptResult>> handler) : McpServerPrompt
    {
        public override Prompt ProtocolPrompt { get; } = new() { Name = name, Description = "stub" };

        public override ValueTask<GetPromptResult> GetAsync(
            RequestContext<GetPromptRequestParams> request,
            CancellationToken cancellationToken) =>
            handler(request);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
