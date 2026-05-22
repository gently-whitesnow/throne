using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var tool = NewWrapper("create_intent", _ => SuccessResult(), sink);

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
        var tool = NewWrapper("create_intent", _ => SuccessResult(), sink, new StubCurrentUser("user-42"));

        await tool.InvokeAsync(NewCallContext("create_intent", new Dictionary<string, JsonElement>()), CancellationToken.None);

        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e => e.UserId == "user-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "InvokeAsync конвертирует ApiException в CallToolResult(IsError=true) с code/message и пишет audit")]
    public async Task Invoke_returns_error_result_on_api_exception()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var tool = NewWrapper(
            "get_intent",
            _ => throw new ApiException(ErrorCodes.IntentNotFound, "Intent 'abc' not found."),
            sink);

        var ctx = NewCallContext("get_intent", new Dictionary<string, JsonElement>
        {
            ["intent_id"] = JsonDocument.Parse("\"abc\"").RootElement,
        });

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        AssertErrorResult(result, ErrorCodes.IntentNotFound, "Intent 'abc' not found.");
        await AssertErrorAuditAsync(sink, "get_intent", ErrorCodes.IntentNotFound, "Intent 'abc' not found.", typeof(ApiException), "abc");
    }

    [Fact(DisplayName = "InvokeAsync сериализует ApiException.Extensions в StructuredContent.data — хинт/query_preview не теряются")]
    public async Task Invoke_returns_api_exception_extensions_in_structured_data()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var extensions = new Dictionary<string, object?>
        {
            ["intent_id"] = "intent-xyz",
            ["query_preview"] = "раз два",
            ["hint"] = "use search_intent_text or extend old_text with neighbouring context to make it unique",
        };
        var tool = NewWrapper(
            "replace_intent_text",
            _ => throw new ApiException(ErrorCodes.IntentTextMatchNotFound, "old_text was not found in Intent.text.", extensions),
            sink);

        var ctx = NewCallContext("replace_intent_text", new Dictionary<string, JsonElement>
        {
            ["intent_id"] = JsonDocument.Parse("\"intent-xyz\"").RootElement,
        });

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        result.IsError.Should().BeTrue();
        ReadStructuredString(result, "code").Should().Be(ErrorCodes.IntentTextMatchNotFound);
        ReadStructuredString(result, "message").Should().Be("old_text was not found in Intent.text.");
        ReadFirstText(result).Should().Be("old_text was not found in Intent.text.");

        var structured = result.StructuredContent!.Value;
        structured.TryGetProperty("data", out var data).Should().BeTrue("ApiException.Extensions должны попадать в StructuredContent.data");
        data.GetProperty("intent_id").GetString().Should().Be("intent-xyz");
        data.GetProperty("query_preview").GetString().Should().Be("раз два");
        data.GetProperty("hint").GetString().Should().Contain("search_intent_text");
    }

    [Fact(DisplayName = "InvokeAsync конвертирует unhandled Exception в CallToolResult(IsError=true) с internal_error и пишет audit")]
    public async Task Invoke_returns_error_result_on_unhandled_exception()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        const string MissingParamMsg = "The arguments dictionary is missing a value for the required parameter 'new_text'.";
        var tool = NewWrapper(
            "replace_intent_text",
            _ => throw new ArgumentException(MissingParamMsg, "arguments"),
            sink);

        var result = await tool.InvokeAsync(
            NewCallContext("replace_intent_text", new Dictionary<string, JsonElement>()),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        ReadStructuredString(result, "code").Should().Be("internal_error");
        ReadStructuredString(result, "message").Should().Contain(MissingParamMsg);
        ReadFirstText(result).Should().Contain(MissingParamMsg);
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.Outcome == McpCallOutcome.Error &&
                e.ErrorCode == "internal_error" &&
                e.ErrorMessage != null &&
                e.ErrorMessage.Contains("missing a value for the required parameter 'new_text'") &&
                e.ExceptionType == typeof(ArgumentException).FullName),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "AddThroneTool сохраняет CallToolResult как native MCP content при прямом AIFunction-вызове")]
    public async Task AddThroneTool_preserves_raw_call_tool_result()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        using var provider = BuildProvider<NativeContentTool>(sink);
        var tool = provider.GetRequiredService<McpServerTool>();

        var result = await tool.InvokeAsync(
            NewCallContext("native_content", new Dictionary<string, JsonElement>()),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<ImageContentBlock>();
        result.StructuredContent.Should().BeNull();
    }

    private static ServiceProvider BuildProvider<TTool>(IMcpCallLogSink sink)
        where TTool : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(sink);
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUser(CurrentUserIds.LocalDev));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        services.AddSingleton<ILogger<AuditingMcpServerTool>>(NullLogger<AuditingMcpServerTool>.Instance);
        services.AddSingleton(new ServerVersion("test"));
        services.AddThroneTool<TTool>();
        return services.BuildServiceProvider();
    }

    private static void AssertErrorResult(CallToolResult result, string expectedCode, string expectedMessage)
    {
        result.IsError.Should().BeTrue();
        ReadFirstText(result).Should().Be(expectedMessage);
        ReadStructuredString(result, "code").Should().Be(expectedCode);
        ReadStructuredString(result, "message").Should().Be(expectedMessage);
    }

    private static Task AssertErrorAuditAsync(
        IMcpCallLogSink sink,
        string toolName,
        string code,
        string message,
        Type exceptionType,
        string? intentId) =>
        sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == toolName &&
                e.Outcome == McpCallOutcome.Error &&
                e.ErrorCode == code &&
                e.ErrorMessage == message &&
                e.ExceptionType == exceptionType.FullName &&
                e.IntentId == intentId),
            Arg.Any<CancellationToken>());

    private static string? ReadFirstText(CallToolResult result) =>
        result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;

    private static string? ReadStructuredString(CallToolResult result, string property)
    {
        if (result.StructuredContent is not { } element || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!element.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return prop.GetString();
    }

    [Fact(DisplayName = "InvokeAsync пробрасывает OperationCanceledException когда токен отменён")]
    public async Task Invoke_propagates_cancellation()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var tool = NewWrapper("create_intent", _ => throw new OperationCanceledException(), sink);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await tool.InvokeAsync(NewCallContext("create_intent", new Dictionary<string, JsonElement>()), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "InvokeAsync проглатывает sink failure и не ломает inner-вызов")]
    public async Task Invoke_swallows_sink_errors()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        sink.WriteAsync(Arg.Any<McpCallLogEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("sink down"));

        var tool = NewWrapper("create_intent", _ => SuccessResult(), sink);

        var ctx = NewCallContext("create_intent", new Dictionary<string, JsonElement>());

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact(DisplayName = "InvokeAsync пишет InstructionBundleUse projection для get_instruction_bundle")]
    public async Task Invoke_summarizes_instruction_bundle_use()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var tool = NewWrapper("get_instruction_bundle", _ => InstructionBundleResult(), sink);

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
        string toolName,
        Func<AIFunctionArguments, object?> handler,
        IMcpCallLogSink sink,
        ICurrentUserAccessor? currentUser = null) =>
        new(
            new StubMcpServerTool(toolName, hasOutputSchema: true),
            new StubAIFunction(toolName, handler),
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
        IDictionary<string, JsonElement> arguments)
    {
        var server = Substitute.For<McpServer>();
        server.SessionId.Returns("session-1");
        var jsonRpc = new JsonRpcRequest { Method = "tools/call", JsonRpc = "2.0", Id = new RequestId("1") };
        var parameters = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = arguments,
        };
        return new RequestContext<CallToolRequestParams>(server, jsonRpc, parameters);
    }

    private static CallToolResult SuccessResult() => new()
    {
        Content = [new TextContentBlock { Text = "{\"ok\":true}" }],
        StructuredContent = JsonSerializer.SerializeToElement(new JsonObject { ["ok"] = true }),
        IsError = false,
    };

    // StructuredContent уже compact на источнике (см. InstructionBundleRenderer и ADR-0003 §8) —
    // никаких `text` в инструкциях, McpResultSummarizer не выкидывает поля, а просто десериализует.
    private static CallToolResult InstructionBundleResult() => new()
    {
        Content = [new TextContentBlock { Text = "mode=work intent_id=intent_123\n\n===== system:common (v1, id=instr_common_1) =====\n\nfull common text\n" }],
        StructuredContent = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["intent_id"] = "intent_123",
            ["mode"] = "work",
            ["instructions"] = new JsonArray
            {
                new JsonObject
                {
                    ["scope"] = "system",
                    ["kind"] = "common",
                    ["instruction_id"] = "instr_common_1",
                    ["current_version"] = 2,
                },
                new JsonObject
                {
                    ["scope"] = "user",
                    ["kind"] = "work",
                    ["instruction_id"] = "instr_light_1",
                    ["current_version"] = 4,
                },
            },
            ["missing_kinds"] = new JsonArray(),
        }),
        IsError = false,
    };

    private static bool HasInstructionRefs(IReadOnlyDictionary<string, object?>? summary)
    {
        if (summary is null ||
            !summary.TryGetValue("instructions", out var instructions) ||
            instructions is not JsonElement element ||
            element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var refs = element.EnumerateArray().ToList();
        refs.Should().HaveCount(2);
        refs[0].GetProperty("kind").GetString().Should().Be("common");
        refs[0].GetProperty("instruction_id").GetString().Should().Be("instr_common_1");
        refs[0].GetProperty("current_version").GetInt32().Should().Be(2);
        refs[0].TryGetProperty("text", out _).Should().BeFalse("StructuredContent для bundle не должен нести instruction text");
        refs[1].GetProperty("kind").GetString().Should().Be("work");
        refs[1].GetProperty("current_version").GetInt32().Should().Be(4);
        return true;
    }

    private sealed class StubMcpServerTool(string name, bool hasOutputSchema) : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new()
        {
            Name = name,
            Description = "stub",
            InputSchema = JsonDocument.Parse("""{ "type": "object" }""").RootElement,
            OutputSchema = hasOutputSchema
                ? JsonDocument.Parse("""{ "type": "object" }""").RootElement
                : null,
        };

        public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();

        public override ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "AuditingMcpServerTool должен инвокать AIFunction напрямую, минуя SDK-обёртку.");
    }

    private sealed class StubAIFunction(string name, Func<AIFunctionArguments, object?> handler) : AIFunction
    {
        public override string Name { get; } = name;

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<object?>(handler(arguments));
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [McpServerToolType]
    private sealed class NativeContentTool
    {
        private readonly byte[] _bytes = [1, 2, 3];

        [McpServerTool(Name = "native_content", ReadOnly = true, UseStructuredContent = false)]
        public CallToolResult GetNativeContent() => new()
        {
            Content =
            [
                new ImageContentBlock
                {
                    Data = _bytes,
                    MimeType = "image/png",
                },
            ],
            IsError = false,
        };
    }
}
