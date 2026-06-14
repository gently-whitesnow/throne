using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Throne.Api.Mcp;
using Throne.Api.Mcp.Tools;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Application.PromptParts;

namespace Throne.Api.Tests.Mcp;

/// <summary>
/// End-to-end gates на §8.1 wire-policy через <see cref="AuditingMcpServerTool"/>:
/// prompt-like tools возвращают <see cref="McpToolPayload"/>, wire
/// <see cref="CallToolResult.StructuredContent"/> приходит null, а refs всё ещё
/// попадают в audit-канал через OOB envelope. <see cref="McpToolWireShapeTests"/>
/// валидирует рендереры в изоляции; здесь — после распаковки envelope в audit
/// wrapper'е. Также фиксируем wire-size budget для bundle.
/// </summary>
public class McpAuditPayloadWireTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ADR-0003 §8.1: McpToolPayload — wire StructuredContent=null, audit summary из OOB envelope")]
    public async Task Invoke_unwraps_payload_nulls_structured_content_and_routes_audit_summary()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var auditSummary = new Dictionary<string, object?>
        {
            ["intent_id"] = "intent-xyz",
            ["current_version"] = 42,
        };
        var payload = new McpToolPayload(
            Wire: new CallToolResult
            {
                Content = [new TextContentBlock { Text = "Полный текст intent с кириллицей.\n" }],
                StructuredContent = null,
                IsError = false,
            },
            AuditSummary: auditSummary);
        var tool = NewWrapper("get_intent", _ => payload, sink);

        var result = await tool.InvokeAsync(
            NewCallContext("get_intent", new Dictionary<string, JsonElement>
            {
                ["intent_id"] = JsonDocument.Parse("\"intent-xyz\"").RootElement,
            }),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent.Should().BeNull(
            "ADR-0003 §8.1: prompt-like tools ship wire StructuredContent=null чтобы Claude Code не прятал Content[] от модели");
        ReadFirstText(result).Should().Contain("Полный текст intent с кириллицей.");
        await sink.Received(1).WriteAsync(
            Arg.Is<McpCallLogEntry>(e =>
                e.ToolName == "get_intent" &&
                e.Outcome == McpCallOutcome.Success &&
                AuditSummaryEquals(e.ResultSummary, "intent_id", "intent-xyz") &&
                AuditSummaryHasKey(e.ResultSummary, "current_version")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ADR-0003 §8.1: 8 prompt-like рендереров через AuditingMcpServerTool — wire StructuredContent=null end-to-end")]
    public async Task Renderers_produce_null_structured_content_through_audit_wrapper()
    {
        var sink = Substitute.For<IMcpCallLogSink>();
        var bundle = new PromptBundle(
            Mode: "work",
            IntentId: "intent-1",
            Parts:
            [
                new PromptBundlePart("system", "common", "part-sys", 1, "Системный common."),
                new PromptBundlePart("user", "work", "part-user", 7, "User work тело."),
            ],
            MissingKeys: []);
        var renderers = new Func<object?>[]
        {
            () => PromptBundleRenderer.Render(bundle),
            () => IntentReadResultRenderer.Render(new McpIntentReadResult(
                "intent-1", "Body.", "work", 1, [], "K", Now, Now, [], [], [])),
            () => TextSliceRenderer.Render(new TextSlice(1, 1, 1, 1, "x", false, null)),
            () => AttachmentTextSliceRenderer.Render(new IntentAttachmentTextSlice("text/plain", 1, 0, 1, false, "x")),
            () => TextSearchResultRenderer.Render(new TextSearchResult([], null)),
            () => CurrentPromptPartRenderer.Render(new CurrentPromptPartView("part", "user", "work", "body", 1, Now)),
            () => PromptPartPatchRenderer.RenderProposed(NewPatch()),
            () => PromptPartPatchRenderer.RenderList([NewPatch()], null),
        };

        foreach (var make in renderers)
        {
            var tool = NewWrapper("prompt_like_tool", _ => make(), sink);
            var result = await tool.InvokeAsync(
                NewCallContext("prompt_like_tool", new Dictionary<string, JsonElement>()),
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().BeNull(
                "каждый prompt-like renderer обязан возвращать McpToolPayload c null wire StructuredContent");
            result.Content.Should().NotBeNullOrEmpty("Content[] — единственный канал текста до модели");
        }
    }

    [Fact(DisplayName = "Wire-size budget: get_prompt_bundle (system+user × common+work) < 30 KB end-to-end")]
    public async Task PromptBundle_wire_size_below_budget()
    {
        // Acceptance из intent ef25f66c: bundle штатного набора укладывается в 30 KB на wire.
        // До амендмента §8.1 цифра уплывала за 70 KB из-за дубля payload в Content +
        // StructuredContent с \uXXXX-эскейпом кириллицы.
        var sink = Substitute.For<IMcpCallLogSink>();
        var bundle = new PromptBundle(
            Mode: "work",
            IntentId: "intent-9",
            Parts:
            [
                new PromptBundlePart("system", "common", "part-sys-common", 1, MakeRussianInstruction("system common", 1000)),
                new PromptBundlePart("system", "work", "part-sys-work", 1, MakeRussianInstruction("system work", 1000)),
                new PromptBundlePart("user", "common", "part-user-common", 1, MakeRussianInstruction("user common", 1000)),
                new PromptBundlePart("user", "work", "part-user-work", 1, MakeRussianInstruction("user work", 1000)),
            ],
            MissingKeys: []);

        var tool = NewWrapper("get_prompt_bundle", _ => PromptBundleRenderer.Render(bundle), sink);
        var result = await tool.InvokeAsync(
            NewCallContext("get_prompt_bundle", new Dictionary<string, JsonElement>
            {
                ["mode"] = JsonDocument.Parse("\"work\"").RootElement,
            }),
            CancellationToken.None);

        var wireBytes = JsonSerializer.SerializeToUtf8Bytes(result);
        wireBytes.Length.Should().BeLessThan(30 * 1024,
            $"ADR-0003 §8.1: bundle wire size must stay under 30 KB (actual: {wireBytes.Length} bytes)");
    }

    private static string MakeRussianInstruction(string label, int chars)
    {
        var sb = new StringBuilder(chars + 64);
        sb.Append("# ").Append(label).Append('\n');
        const string body = "Краткое описание инструкции с русским текстом и переносами строк. ";
        while (sb.Length < chars)
        {
            sb.Append(body);
        }
        return sb.ToString();
    }

    private static McpPromptPartPatchReadModel NewPatch() => new(
        Id: "patch-1",
        TargetScope: "user",
        TargetKey: "work",
        Status: "proposed",
        Operation: "replace_text",
        PatchText: "body",
        ModeRoles: null,
        AppliedText: null,
        EvidenceCardIds: [],
        Rationale: "r",
        RejectComment: null,
        BaseVersion: 1,
        AppliedVersion: null,
        CreatedAt: Now,
        UpdatedAt: Now,
        DecidedAt: null);

    private static bool AuditSummaryEquals(IReadOnlyDictionary<string, object?>? summary, string key, string expected)
    {
        if (summary is null || !summary.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }
        return raw is string s ? s == expected : raw.ToString() == expected;
    }

    private static bool AuditSummaryHasKey(IReadOnlyDictionary<string, object?>? summary, string key) =>
        summary?.ContainsKey(key) == true;

    private static string? ReadFirstText(CallToolResult result) =>
        result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;

    private static AuditingMcpServerTool NewWrapper(
        string toolName,
        Func<AIFunctionArguments, object?> handler,
        IMcpCallLogSink sink) =>
        new(
            new StubTool(toolName),
            new StubAIFunction(toolName, handler),
            sink,
            new FakeTimeProvider(Now),
            NullLogger<AuditingMcpServerTool>.Instance,
            new ServerVersion("test"));

    private static RequestContext<CallToolRequestParams> NewCallContext(
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var server = Substitute.For<McpServer>();
        server.SessionId.Returns("session-1");
        var jsonRpc = new JsonRpcRequest { Method = "tools/call", JsonRpc = "2.0", Id = new RequestId("1") };
        return new RequestContext<CallToolRequestParams>(
            server, jsonRpc, new CallToolRequestParams { Name = toolName, Arguments = arguments });
    }


    private sealed class StubTool(string name) : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new()
        {
            Name = name,
            Description = "stub",
            InputSchema = JsonDocument.Parse("""{ "type": "object" }""").RootElement,
            OutputSchema = JsonDocument.Parse("""{ "type": "object" }""").RootElement,
        };

        public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();

        public override ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("AuditingMcpServerTool invokes AIFunction directly.");
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
}
