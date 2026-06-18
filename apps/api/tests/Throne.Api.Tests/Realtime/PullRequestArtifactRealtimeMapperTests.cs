using System.Text.Json;
using FluentAssertions;
using Throne.Api.Realtime;
using Throne.Application.Events;
using Throne.Domain.Repositories;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Tests.Realtime;

public class PullRequestArtifactRealtimeMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact(DisplayName = "pull_request.artifact_updated: payload — pointer на binding/type")]
    public void Maps_artifact_updated_to_pointer_payload()
    {
        var artifact = PullRequestArtifact.Create(
            PullRequestArtifactId.New(),
            new BindingId("binding-1"),
            42,
            "static_analysis",
            PullRequestArtifactRenderNames.Markdown,
            "# body",
            "Static analysis",
            PullRequestArtifactSourceNames.Static,
            ["sha:abc"],
            Now);

        var envelope = PullRequestArtifactRealtimeMapper.TryMap(new PullRequestArtifactUpdated(artifact));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.PullRequestArtifactUpdated);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("binding_id", "pull_request_number", "type", "produced_at");
        root.GetProperty("binding_id").GetString().Should().Be("binding-1");
        root.GetProperty("pull_request_number").GetInt32().Should().Be(42);
        root.GetProperty("type").GetString().Should().Be("static_analysis");
        root.GetProperty("produced_at").GetDateTimeOffset().Should().Be(Now);
    }

    private static JsonDocument SerializePayload(object payload)
    {
        var json = JsonSerializer.Serialize(payload, PayloadOptions);
        return JsonDocument.Parse(json);
    }
}
