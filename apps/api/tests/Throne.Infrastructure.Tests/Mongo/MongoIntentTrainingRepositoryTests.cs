using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
public class MongoIntentTrainingRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WrittenAt = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "AddQaAsync вставляет qa и обновляет updated_at без инкремента current_version")]
    public async Task AddQa_inserts_and_bumps_updated_at()
    {
        var (db, repo, uow, id) = await SeedAsync();

        var qa = IntentQa.Create("qa-1", id, intentVersionAtWrite: 1,
            "why?", "because", WrittenAt, IntentTrainingAuthor.Agent);

        var outcome = await uow.ExecuteAsync(
            ct => repo.AddQaAsync(id, expectedVersion: 1, qa, WrittenAt, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<AppendTrainingOutcome.Appended>();

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(d => d.Id == id.Value).FirstOrDefaultAsync();
        stored!.CurrentVersion.Should().Be(1);
        stored.UpdatedAt.Should().Be(WrittenAt.UtcDateTime);

        var qaDocs = await db.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa)
            .Find(d => d.IntentId == id.Value).ToListAsync();
        qaDocs.Should().HaveCount(1);
        qaDocs[0].Question.Should().Be("why?");
        qaDocs[0].Answer.Should().Be("because");
        qaDocs[0].IntentVersionAtWrite.Should().Be(1);
        qaDocs[0].CreatedBy.Should().Be("agent");
    }

    [Fact(DisplayName = "AddQaAsync с неверным expected_version возвращает VersionConflict без side-effects")]
    public async Task AddQa_version_mismatch_returns_conflict()
    {
        var (db, repo, uow, id) = await SeedAsync();

        var qa = IntentQa.Create("qa-x", id, intentVersionAtWrite: 99,
            "q", "a", WrittenAt, IntentTrainingAuthor.Agent);

        var outcome = await uow.ExecuteAsync(
            ct => repo.AddQaAsync(id, expectedVersion: 99, qa, WrittenAt, ct),
            CancellationToken.None);

        var conflict = outcome.Should().BeOfType<AppendTrainingOutcome.VersionConflict>().Subject;
        conflict.CurrentVersion.Should().Be(1);

        var qaDocs = await db.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa)
            .Find(d => d.IntentId == id.Value).ToListAsync();
        qaDocs.Should().BeEmpty();
    }

    [Fact(DisplayName = "AddQaAsync на несуществующем Intent возвращает NotFound")]
    public async Task AddQa_missing_intent_returns_not_found()
    {
        var (_, repo, uow, _) = await SeedAsync();

        var ghost = new IntentId("does-not-exist");
        var qa = IntentQa.Create("qa-x", ghost, 1, "q", "a", WrittenAt, IntentTrainingAuthor.Agent);

        var outcome = await uow.ExecuteAsync(
            ct => repo.AddQaAsync(ghost, expectedVersion: 1, qa, WrittenAt, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<AppendTrainingOutcome.NotFound>();
    }

    [Fact(DisplayName = "AddReviewAsync вставляет review и обновляет updated_at без инкремента current_version")]
    public async Task AddReview_inserts_and_bumps_updated_at()
    {
        var (db, repo, uow, id) = await SeedAsync();

        var review = IntentReview.Create("rev-1", id, 1, "n", "r", WrittenAt, IntentTrainingAuthor.Agent);

        var outcome = await uow.ExecuteAsync(
            ct => repo.AddReviewAsync(id, expectedVersion: 1, review, WrittenAt, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<AppendTrainingOutcome.Appended>();

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(d => d.Id == id.Value).FirstOrDefaultAsync();
        stored!.CurrentVersion.Should().Be(1);
        stored.UpdatedAt.Should().Be(WrittenAt.UtcDateTime);

        var reviewDocs = await db.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview)
            .Find(d => d.IntentId == id.Value).ToListAsync();
        reviewDocs.Should().HaveCount(1);
        reviewDocs[0].Note.Should().Be("n");
        reviewDocs[0].Reason.Should().Be("r");
    }

    private async Task<(IMongoDatabase Db, MongoIntentTrainingRepository Repo, IUnitOfWork Uow, IntentId Id)> SeedAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var intentRepo = new MongoIntentRepository(db, sessions);
        var trainingRepo = new MongoIntentTrainingRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);

        var id = IntentId.New();
        var intent = Intent.Create(id, "seed", null, Created);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "seed", Created, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => intentRepo.CreateAsync(intent, version, ct), CancellationToken.None);
        return (db, trainingRepo, uow, id);
    }
}
