using System.Text.Json;
using FluentAssertions;
using Throne.Api.Realtime;
using Throne.Application.Events;
using Throne.Domain.Repositories;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Tests.Realtime;

/// <summary>
/// Smoke-проверки mapper'а реестра репозиториев (ADR-0031): два события
/// (<c>repository.registered</c>, <c>repository.document_updated</c>) должны укладываться
/// в форму из <c>specs/contracts/realtime/events.yaml</c>. JSON-сериализация идёт теми же
/// опциями, что использует <see cref="RealtimeController"/>, чтобы snake_case-ключи и
/// строковый <c>provider</c> провалидировались на wire-уровне.
/// </summary>
public class RepositoryRegistryRealtimeMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoCoordinate Coordinate = new(GitProviderNames.GitHub, "octo", "throne");

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact(DisplayName = "repository.registered: payload — полный RepositoryDto")]
    public void Maps_registered_to_repository_dto()
    {
        var repository = Repository.Create(RepositoryId.New(), Coordinate, Now);

        var envelope = RepositoryRegistryRealtimeMapper.TryMap(new RepositoryRegistered(repository));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.RepositoryRegistered);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.GetProperty("provider").GetString().Should().Be("github");
        root.GetProperty("owner").GetString().Should().Be("octo");
        root.GetProperty("repo").GetString().Should().Be("throne");
        root.GetProperty("full_name").GetString().Should().Be(Coordinate.FullName);
    }

    [Fact(DisplayName = "repository.document_updated: pointer-payload {provider, owner, repo, slug, version}")]
    public void Maps_document_updated_to_pointer_payload()
    {
        var artifact = RepositoryArtifact.Create(
            RepositoryArtifactId.New(), Coordinate, "db-schema-map", "DB schema", "# erd",
            RepositoryArtifactRenderHints.SchemaMap, Now);

        var envelope = RepositoryRegistryRealtimeMapper.TryMap(new RepositoryDocumentUpdated(artifact));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.RepositoryDocumentUpdated);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("provider", "owner", "repo", "slug", "version");
        // provider едет голой wire-строкой, не int от generated-enum.
        root.GetProperty("provider").GetString().Should().Be("github");
        root.GetProperty("owner").GetString().Should().Be("octo");
        root.GetProperty("repo").GetString().Should().Be("throne");
        root.GetProperty("slug").GetString().Should().Be("db-schema-map");
        root.GetProperty("version").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "Чужие доменные события возвращают null")]
    public void Returns_null_for_unrelated_event()
    {
        RepositoryRegistryRealtimeMapper.TryMap(new IntentDeleted("x")).Should().BeNull();
    }

    private static JsonDocument SerializePayload(object payload)
    {
        var json = JsonSerializer.Serialize(payload, PayloadOptions);
        return JsonDocument.Parse(json);
    }
}
