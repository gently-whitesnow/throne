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

    [Fact(DisplayName = "CreateAsync пишет user-Instruction в instructions со scope/user_id и v1 snapshot")]
    public async Task Create_persists_canonical_and_v1_snapshot()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var instruction = Instruction.Create(
            InstructionId.New(),
            InstructionScopeNames.User,
            MvpUser.Id,
            InstructionKindNames.Work,
            "work text",
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
        stored!.Kind.Should().Be(InstructionKindNames.Work);
        stored.Scope.Should().Be(InstructionScopeNames.User);
        stored.UserId.Should().Be(MvpUser.Id);
        stored.Text.Should().Be("work text");
        stored.CurrentVersion.Should().Be(1);

        var versions = await db.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions)
            .Find(x => x.OwnerId == instruction.Id.Value).ToListAsync();
        versions.Should().ContainSingle();
        versions[0].OwnerKind.Should().Be("instruction");
        versions[0].Kind.Should().Be("create");
        versions[0].Snapshot.Should().Be("work text");
    }

    [Fact(DisplayName = "GetUserInstructionsByKindsAsync фильтрует по user_id, scope и kind set")]
    public async Task User_instructions_filtered_by_user_scope_and_kinds()
    {
        var (_, repo, uow) = await NewScopeAsync();

        await SeedUserInstructionAsync(repo, uow, MvpUser.Id, InstructionKindNames.Common, "common text");
        await SeedUserInstructionAsync(repo, uow, MvpUser.Id, InstructionKindNames.Work, "work text");
        await SeedUserInstructionAsync(repo, uow, MvpUser.Id, InstructionKindNames.Interview, "interview text");
        await SeedUserInstructionAsync(repo, uow, "other-user", InstructionKindNames.Work, "other work");

        var got = await repo.GetUserInstructionsByKindsAsync(
            MvpUser.Id,
            [InstructionKindNames.Common, InstructionKindNames.Work],
            CancellationToken.None);

        got.Select(i => i.Kind).Should().BeEquivalentTo(
            new[] { InstructionKindNames.Common, InstructionKindNames.Work });
        got.Should().OnlyContain(i => i.UserId == MvpUser.Id && i.Scope == InstructionScopeNames.User);
    }

    [Fact(DisplayName = "GetUserInstructionsByKindsAsync на пустой список kinds возвращает пусто")]
    public async Task User_instructions_empty_kinds_returns_empty()
    {
        var (_, repo, uow) = await NewScopeAsync();
        await SeedUserInstructionAsync(repo, uow, MvpUser.Id, InstructionKindNames.Work, "x");

        var got = await repo.GetUserInstructionsByKindsAsync(MvpUser.Id, [], CancellationToken.None);

        got.Should().BeEmpty();
    }

    private static async Task SeedUserInstructionAsync(
        MongoInstructionRepository repo,
        MongoUnitOfWork uow,
        string userId,
        string kind,
        string text)
    {
        var instruction = Instruction.Create(InstructionId.New(), InstructionScopeNames.User, userId, kind, text, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"),
            TextVersionOwnerKind.Instruction,
            instruction.Id.Value,
            text,
            Now,
            TextVersionAuthor.System);
        await uow.ExecuteAsync(ct => repo.CreateAsync(instruction, version, ct), CancellationToken.None);
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
}
