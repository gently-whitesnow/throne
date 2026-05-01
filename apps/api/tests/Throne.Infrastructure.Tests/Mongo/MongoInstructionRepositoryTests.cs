using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Instructions;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
public class MongoInstructionRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет Instruction в instructions и v1 snapshot в text_versions")]
    public async Task Create_persists_canonical_and_v1_snapshot()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var instruction = Instruction.Create(
            InstructionId.New(),
            InstructionKindNames.LightWork,
            "light text",
            Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"),
            TextVersionOwnerKind.Instruction,
            instruction.Id.Value,
            instruction.Text,
            Now,
            TextVersionAuthor.System);

        await uow.ExecuteAsync(ct => repo.CreateAsync(instruction, version, ct), CancellationToken.None);

        var stored = await db.GetCollection<InstructionDocument>(MongoCollectionNames.Instructions)
            .Find(x => x.Id == instruction.Id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Kind.Should().Be(InstructionKindNames.LightWork);
        stored.Text.Should().Be("light text");
        stored.CurrentVersion.Should().Be(1);

        var versions = await db.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions)
            .Find(x => x.OwnerId == instruction.Id.Value).ToListAsync();
        versions.Should().ContainSingle();
        versions[0].OwnerKind.Should().Be("instruction");
        versions[0].Kind.Should().Be("create");
        versions[0].Snapshot.Should().Be("light text");
    }

    [Fact(DisplayName = "EnsureSeedInstructions idempotently создаёт четыре seed-инструкции")]
    public async Task Seed_bootstrap_is_idempotent()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var handler = new EnsureSeedInstructionsHandler(repo, uow, new FakeTimeProvider(Now));

        await handler.HandleAsync(CancellationToken.None);
        await handler.HandleAsync(CancellationToken.None);

        var instructions = await db.GetCollection<InstructionDocument>(MongoCollectionNames.Instructions)
            .Find(_ => true).ToListAsync();
        instructions.Should().HaveCount(4);
        instructions.Select(x => x.Kind).Should().BeEquivalentTo(InstructionKindNames.All);

        var versions = await db.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions)
            .Find(x => x.OwnerKind == "instruction").ToListAsync();
        versions.Should().HaveCount(4);
    }

    private async Task<(IMongoDatabase Db, MongoInstructionRepository Repo, MongoUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_instruction_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoInstructionRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (db, repo, uow);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
