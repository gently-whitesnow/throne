using System.Text.Json;
using FluentAssertions;
using Throne.Api.Mcp.Tools;

namespace Throne.Api.Tests.Mcp;

public class McpStructuredToolShapeTests
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact(DisplayName = "get_intent: sort_key не попадает в model-facing текст")]
    public void IntentRead_text_omits_sort_key()
    {
        var payload = IntentReadResultRenderer.Render(new McpIntentReadResult(
            Id: "intent-42",
            Text: "Body.",
            Status: "work",
            CurrentVersion: 3,
            Tags: [],
            SortKey: "0000A",
            CreatedAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt: DateTimeOffset.Parse("2026-05-22T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            Attachments: [],
            Links: [],
            Repositories: []));

        ReadText(payload).Should().NotContain("sort_key=");
        payload.AuditSummary.Should().ContainKey("sort_key");
    }

    [Fact(DisplayName = "list_intents: structured payload не содержит sort_key")]
    public void IntentList_payload_omits_sort_key()
    {
        var result = new McpIntentListResult(
            Items:
            [
                new McpIntentListItem(
                    Id: "intent-42",
                    Status: "work",
                    CurrentVersion: 3,
                    Tags: [],
                    Preview: "Body.",
                    CreatedAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    UpdatedAt: DateTimeOffset.Parse("2026-05-22T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            ],
            NextCursor: null);

        Serialize(result).Should().NotContain("sort_key").And.NotContain("sortKey").And.NotContain("SortKey");
    }

    [Fact(DisplayName = "list_intent_links: peer structured payload не содержит sort_key")]
    public void IntentLinks_payload_omits_peer_sort_key()
    {
        var result = new McpIntentLinksPageResult(
            Items:
            [
                new McpIntentLinkRead(
                    Id: "link-1",
                    Direction: "outgoing",
                    Blocking: false,
                    Author: "agent",
                    Rationale: null,
                    CreatedAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    Peer: new McpIntentLinkPeer(
                        Id: "intent-99",
                        Status: "ready_for_work",
                        CurrentVersion: 1,
                        Preview: "Peer.",
                        Tags: [])),
            ],
            NextCursor: null);

        Serialize(result).Should().NotContain("sort_key").And.NotContain("sortKey").And.NotContain("SortKey");
    }

    private static string ReadText(McpToolPayload payload) =>
        payload.Wire.Content?.OfType<ModelContextProtocol.Protocol.TextContentBlock>().FirstOrDefault()?.Text
        ?? string.Empty;

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, WireOptions);
}
