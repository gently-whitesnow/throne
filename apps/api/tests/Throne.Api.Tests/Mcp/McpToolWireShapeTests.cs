using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using Throne.Api.Mcp.Tools;
using Throne.Application.Intents;
using Throne.Application.PromptPartPatches;
using Throne.Application.PromptParts;
using Throne.Domain.Intents;

namespace Throne.Api.Tests.Mcp;

/// <summary>
/// Regression-гейт на MCP wire-policy (ADR-0003 §8.1, 2026-05 amendment). Для prompt-like
/// tools на wire едет только Content[]: <c>CallToolResult.StructuredContent</c> обязан
/// быть <c>null</c>. Compact refs ушли в audit-канал через <c>McpToolPayload.AuditSummary</c>
/// — там <c>text</c> / <c>content</c> / <c>patch_text</c> / <c>applied_text</c> /
/// <c>current_part_text</c> / <c>base_part_text</c> / <c>rationale</c> /
/// <c>note</c> также запрещены.
///
/// Тест ловит регрессию, симметричную 9cc71a8c… (дубль payload) и 6e96cd22… (compact
/// StructuredContent всё равно перехватывает рендер у structured-aware клиентов).
/// </summary>
public class McpToolWireShapeTests
{
    private static readonly HashSet<string> BannedFields = new(StringComparer.Ordinal)
    {
        "text",
        "content",
        "patch_text",
        "applied_text",
        "current_part_text",
        "base_part_text",
        "rationale",
        "note",
    };

    [Fact(DisplayName = "PromptBundleRenderer: wire StructuredContent=null, refs только в audit")]
    public void PromptBundle_wire_has_null_structured_content()
    {
        var bundle = new PromptBundle(
            Mode: "work",
            IntentId: "intent-1",
            Parts:
            [
                new PromptBundlePart("system", "common", "part-sys-common", 1, "Большой системный текст с кириллицей."),
                new PromptBundlePart("user", "work", "part-user-work", 7, "User work prompt body."),
            ],
            MissingKeys: []);

        var payload = PromptBundleRenderer.Render(bundle);

        ReadText(payload.Wire).Should().Contain("Большой системный текст с кириллицей.");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "PromptBundleRenderer сохраняет legacy-visible headers при сохранённых ids")]
    public void PromptBundle_text_keeps_legacy_visible_headers()
    {
        var bundle = new PromptBundle(
            Mode: "work",
            IntentId: "intent-1",
            Parts:
            [
                new PromptBundlePart("system", "common", "system:common", 1, "sys common"),
                new PromptBundlePart("user", "work", "i-work", 4, "legacy user work"),
            ],
            MissingKeys: []);

        var text = PromptBundleRenderer.RenderText(bundle);

        text.Should().Be(
            "mode=work intent_id=intent-1\n" +
            "\n===== system:common (v1, id=system:common) =====\n\n" +
            "sys common\n" +
            "\n===== user:work (v4, id=i-work) =====\n\n" +
            "legacy user work\n");
    }

    [Fact(DisplayName = "IntentReadResultRenderer: Intent.text в Content, wire StructuredContent=null")]
    public void IntentReadResult_wire_has_null_structured_content()
    {
        var result = new McpIntentReadResult(
            Id: "intent-42",
            Text: "Большой текст Intent с переносами\nстрок и кириллицей.",
            Status: "work",
            CurrentVersion: 3,
            Tags: [new McpTagRef("tag-1", "throne")],
            SortKey: "0000A",
            CreatedAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt: DateTimeOffset.Parse("2026-05-22T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            Attachments: [],
            Links: [],
            Repositories: []);

        var payload = IntentReadResultRenderer.Render(result);

        ReadText(payload.Wire).Should().Contain("Большой текст Intent");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "TextSliceRenderer: содержимое slice в Content, wire StructuredContent=null")]
    public void TextSlice_wire_has_null_structured_content()
    {
        var slice = new TextSlice(
            CurrentVersion: 4,
            StartLine: 1,
            EndLine: 3,
            TotalLines: 10,
            Content: "линия 1\nлиния 2\nлиния 3",
            Truncated: true,
            NextStartLine: 4);

        var payload = TextSliceRenderer.Render(slice);

        ReadText(payload.Wire).Should().Contain("линия 1");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "AttachmentTextSliceRenderer: decoded text в Content, wire StructuredContent=null")]
    public void AttachmentSlice_wire_has_null_structured_content()
    {
        var slice = new IntentAttachmentTextSlice(
            ContentType: "text/plain",
            TotalSizeBytes: 1024,
            ReturnedBytesStart: 0,
            ReturnedBytesEnd: 100,
            Truncated: true,
            Text: "Большой fragment attachment текста.");

        var payload = AttachmentTextSliceRenderer.Render(slice);

        ReadText(payload.Wire).Should().Contain("Большой fragment attachment текста.");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "TextSearchResultRenderer: context excerpts в Content, wire StructuredContent=null")]
    public void TextSearch_wire_has_null_structured_content()
    {
        var search = new TextSearchResult(
            Matches:
            [
                new TextSearchMatch(
                    MatchLine: 42,
                    MatchColumn: 5,
                    Context: "контекстная строка с совпадением",
                    ContextStartLine: 39),
            ],
            TotalMatchesEstimate: null);

        var payload = TextSearchResultRenderer.Render(search);

        ReadText(payload.Wire).Should().Contain("контекстная строка с совпадением");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "CurrentPromptPartRenderer: prompt-part.text в Content, wire StructuredContent=null")]
    public void CurrentPromptPart_wire_has_null_structured_content()
    {
        var view = new CurrentPromptPartView(
            PromptPartId: "part-1",
            Scope: "user",
            Key: "work",
            Text: "Полный текст инструкции.",
            CurrentVersion: 9,
            UpdatedAt: DateTimeOffset.Parse("2026-05-22T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var payload = CurrentPromptPartRenderer.Render(view);

        ReadText(payload.Wire).Should().Contain("Полный текст инструкции.");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "PromptPartPatchRenderer.RenderDetail: все тела patch в Content, wire StructuredContent=null")]
    public void PromptPartPatchDetail_wire_has_null_structured_content()
    {
        var patch = NewPatch();
        var view = new PromptPartPatchView(
            Patch: null!,
            CurrentPartText: "Current prompt part body.",
            CurrentPartVersion: 5,
            BaseVersionMatchesCurrent: false,
            BasePartText: "Base prompt part body at v4.");

        var payload = PromptPartPatchRenderer.RenderDetail(patch, view);

        var text = ReadText(payload.Wire);
        text.Should().Contain("Patch body proposal.");
        text.Should().Contain("Applied body after edit.");
        text.Should().Contain("Current prompt part body.");
        text.Should().Contain("Base prompt part body at v4.");
        text.Should().Contain("Why this matters.");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "PromptPartPatchRenderer.RenderProposed: patch_text/applied_text/rationale в Content, wire StructuredContent=null")]
    public void PromptPartPatchProposed_wire_has_null_structured_content()
    {
        var payload = PromptPartPatchRenderer.RenderProposed(NewPatch());

        ReadText(payload.Wire).Should().Contain("Patch body proposal.");
        AssertPromptLikeWireShape(payload);
    }

    [Fact(DisplayName = "PromptPartPatchRenderer.RenderList: тело каждого patch в Content, wire StructuredContent=null")]
    public void PromptPartPatchList_wire_has_null_structured_content()
    {
        var payload = PromptPartPatchRenderer.RenderList([NewPatch(), NewPatch("Second body.")], nextCursor: "cursor-2");

        var text = ReadText(payload.Wire);
        text.Should().Contain("Patch body proposal.");
        text.Should().Contain("Second body.");
        AssertPromptLikeWireShape(payload);
    }

    private static McpPromptPartPatchReadModel NewPatch(string body = "Patch body proposal.") => new(
        Id: "patch-1",
        TargetScope: "user",
        TargetKey: "work",
        Status: "proposed",
        Operation: "replace_text",
        PatchText: body,
        ModeRoles: null,
        AppliedText: "Applied body after edit.",
        EvidenceCardIds: ["ev-1", "ev-2"],
        Rationale: "Why this matters.",
        RejectComment: null,
        BaseVersion: 4,
        AppliedVersion: 5,
        CreatedAt: DateTimeOffset.Parse("2026-05-22T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        UpdatedAt: DateTimeOffset.Parse("2026-05-22T11:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        DecidedAt: null);

    private static string ReadText(CallToolResult result) =>
        result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;

    private static void AssertPromptLikeWireShape(McpToolPayload payload)
    {
        payload.Wire.StructuredContent.Should().BeNull(
            "ADR-0003 §8.1 (2026-05 amendment): wire StructuredContent must be null for prompt-like tools — иначе structured-aware клиенты прячут Content[] от модели");
        payload.AuditSummary.Should().NotBeNull("compact refs обязаны попадать в audit-канал через OOB envelope");
        AssertAuditHasNoBannedKeys(payload.AuditSummary!);
    }

    private static void AssertAuditHasNoBannedKeys(IReadOnlyDictionary<string, object?> summary)
    {
        var path = new Stack<string>();
        foreach (var (key, value) in summary)
        {
            BannedFields.Contains(key).Should().BeFalse(
                $"AuditSummary не должен содержать '{key}' (путь: $.{key})");
            path.Push(key);
            AssertNoBannedKeys(value, path);
            path.Pop();
        }
    }

    private static void AssertNoBannedKeys(object? value, Stack<string> path)
    {
        if (value is not JsonElement element)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    BannedFields.Contains(property.Name).Should().BeFalse(
                        $"AuditSummary не должен содержать '{property.Name}' (путь: {RenderPath(path)}.{property.Name})");
                    path.Push(property.Name);
                    AssertNoBannedKeys(property.Value, path);
                    path.Pop();
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    path.Push($"[{index++}]");
                    AssertNoBannedKeys(item, path);
                    path.Pop();
                }
                break;
        }
    }

    private static string RenderPath(Stack<string> path) =>
        path.Count == 0 ? "$" : "$." + string.Join(".", path.Reverse());
}
