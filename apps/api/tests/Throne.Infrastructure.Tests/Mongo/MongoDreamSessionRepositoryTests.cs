using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Dreams;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoDreamSessionRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет DreamSession; GetAsync возвращает её в текущем owner-скоупе")]
    public async Task Create_persists_and_owner_scoped()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");

        var session = MakeSession("d-1", "user-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(session, ct), CancellationToken.None);

        var loaded = await repo.GetAsync("d-1", CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.OwnerUserId.Should().Be("user-1");
        loaded.Payload.Vendor.Should().Be("claude-code");
        loaded.Payload.Summary.Should().Be("hello");
        loaded.Payload.ProcessedConversationIds.Should().Equal("a", "b");
    }

    [Fact(DisplayName = "GetAsync фильтрует по owner и не отдаёт чужие dream-sessions")]
    public async Task Get_filters_by_owner()
    {
        var (_, asUser1, uow) = await NewScopeAsync("user-1");
        var (_, asUser2, _) = await NewScopeAsync("user-2", reuse: true);

        await uow.ExecuteAsync(ct => asUser1.CreateAsync(MakeSession("d-1", "user-1"), ct), CancellationToken.None);

        (await asUser1.GetAsync("d-1", CancellationToken.None)).Should().NotBeNull();
        (await asUser2.GetAsync("d-1", CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "ListAsync owner-scoped + vendor-фильтр + сортировка по created_at DESC")]
    public async Task List_owner_vendor_filter_sorts_desc()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");

        var older = MakeSession("d-1", "user-1", vendor: "claude-code", at: Now);
        var newer = MakeSession("d-2", "user-1", vendor: "codex-cli", at: Now.AddHours(1));
        await uow.ExecuteAsync(ct => repo.CreateAsync(older, ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => repo.CreateAsync(newer, ct), CancellationToken.None);

        var all = await repo.ListAsync(
            new DreamSessionListFilter(Vendor: null),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        all.Items.Should().HaveCount(2);
        all.Items[0].Id.Should().Be("d-2");
        all.Items[1].Id.Should().Be("d-1");

        var onlyCodex = await repo.ListAsync(
            new DreamSessionListFilter(Vendor: "codex-cli"),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        onlyCodex.Items.Should().HaveCount(1);
        onlyCodex.Items[0].Id.Should().Be("d-2");
    }

    private static DreamSession MakeSession(
        string id,
        string ownerUserId,
        string vendor = "claude-code",
        DateTimeOffset? at = null) =>
        DreamSession.Create(
            id: id,
            ownerUserId: ownerUserId,
            createdAt: at ?? Now,
            vendor: vendor,
            dateFrom: null,
            dateTo: null,
            processedConversationIds: ["a", "b"],
            summary: "hello",
            reflection: null,
            proposedPatchIds: []);

    private static IMongoDatabase? _sharedDb;
    private static MongoSessionAccessor? _sharedSession;
    private static MongoUnitOfWork? _sharedUow;

    private async Task<(IMongoDatabase Db, MongoDreamSessionRepository Repo, MongoUnitOfWork Uow)> NewScopeAsync(
        string userId,
        bool reuse = false)
    {
        if (!reuse || _sharedDb is null)
        {
            var name = $"throne_test_{Guid.NewGuid():N}";
            await fixture.Client.DropDatabaseAsync(name);
            _sharedDb = fixture.Client.GetDatabase(name);
            _sharedSession = new MongoSessionAccessor();
            _sharedUow = new MongoUnitOfWork(fixture.Client, _sharedSession);
        }

        var repo = new MongoDreamSessionRepository(_sharedDb!, _sharedSession!, new TestCurrentUserAccessor(userId));
        return (_sharedDb!, repo, _sharedUow!);
    }
}
