using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoPullRequestArtifactStoreTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly BindingId BindingId = new("binding-1");

    [Fact(DisplayName = "UpsertAsync дважды по (binding_id,type) оставляет одну latest-запись")]
    public async Task Upsert_is_latest_wins()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await WriteAsync(scope, "# v1", Now);

        var second = await WriteAsync(scope, "# v2", Now.AddMinutes(1));

        second.Created.Should().BeFalse();
        second.Artifact.Content.Should().Be("# v2");
        second.Artifact.ProducedAt.Should().Be(Now.AddMinutes(1));

        var list = await scope.PullRequestArtifacts.ListAsync(BindingId, CancellationToken.None);
        list.Should().ContainSingle();
        list[0].Content.Should().Be("# v2");
    }

    [Fact(DisplayName = "GetAsync/ListAsync отдают актуальные PR-артефакты по binding")]
    public async Task Get_and_list()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await WriteAsync(scope, "coverage", Now, type: "coverage");
        await WriteAsync(scope, "analysis", Now, type: "static_analysis");

        var found = await scope.PullRequestArtifacts.GetAsync(BindingId, "coverage", CancellationToken.None);
        var list = await scope.PullRequestArtifacts.ListAsync(BindingId, CancellationToken.None);

        found!.Content.Should().Be("coverage");
        list.Select(x => x.Type).Should().Equal("coverage", "static_analysis");
    }

    [Fact(DisplayName = "UpsertAsync сохраняет и перезаписывает head_sha и review_recommendation")]
    public async Task Upsert_round_trips_review_fields()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        var first = ReviewRecommendation.Create([
            new ReviewFileOrderEntry("a.cs", "core", ReviewFileRisk.High),
        ]);
        await scope.PullRequestArtifacts.UpsertAsync(
            BindingId, 42, "review_recommendation", PullRequestArtifactRenderNames.Markdown,
            "# review", "Review", PullRequestArtifactSourceNames.Agent, ["gh pr diff"], Now,
            "sha-1", first, CancellationToken.None);

        var stored = await scope.PullRequestArtifacts.GetAsync(BindingId, "review_recommendation", CancellationToken.None);
        stored!.HeadSha.Should().Be("sha-1");
        stored.ReviewRecommendation!.FileOrder.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Path = "a.cs", Reason = "core", Risk = ReviewFileRisk.High });

        var second = ReviewRecommendation.Create([
            new ReviewFileOrderEntry("b.cs", null, null),
        ]);
        await scope.PullRequestArtifacts.UpsertAsync(
            BindingId, 42, "review_recommendation", PullRequestArtifactRenderNames.Markdown,
            "# review v2", "Review", PullRequestArtifactSourceNames.Agent, ["gh pr diff"], Now.AddMinutes(1),
            "sha-2", second, CancellationToken.None);

        var stored2 = await scope.PullRequestArtifacts.GetAsync(BindingId, "review_recommendation", CancellationToken.None);
        stored2!.HeadSha.Should().Be("sha-2");
        stored2.ReviewRecommendation!.FileOrder.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Path = "b.cs", Reason = (string?)null, Risk = (ReviewFileRisk?)null });
    }

    [Fact(DisplayName = "Уникальный индекс (binding_id,type) запрещает второй документ того же типа")]
    public async Task Binding_type_index_is_unique()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await WriteAsync(scope, "# v1", Now);
        var collection = scope.Database.GetCollection<PullRequestArtifactDocument>(
            MongoCollectionNames.PullRequestArtifacts);

        var act = async () => await collection.InsertOneAsync(new PullRequestArtifactDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            BindingId = BindingId.Value,
            PullRequestNumber = 42,
            Type = "static_analysis",
            Render = PullRequestArtifactRenderNames.Markdown,
            Content = "dup",
            Summary = "dup",
            Source = PullRequestArtifactSourceNames.Static,
            SourceRefs = [],
            ProducedAt = Now.UtcDateTime,
        });

        await act.Should().ThrowAsync<MongoWriteException>();
    }

    private static Task<WritePullRequestArtifactOutcome> WriteAsync(
        RepositoryStoreTestScope scope,
        string content,
        DateTimeOffset producedAt,
        string type = "static_analysis") =>
        scope.PullRequestArtifacts.UpsertAsync(
            BindingId,
            42,
            type,
            PullRequestArtifactRenderNames.Markdown,
            content,
            "Summary",
            PullRequestArtifactSourceNames.Static,
            ["sha:abc"],
            producedAt,
            null,
            null,
            CancellationToken.None);
}
