using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

/// <summary>
/// Регрессионный тест ADR-0012: репозиторий не должен возвращать или мутировать
/// записи чужого OwnerUserId.
/// </summary>
[Collection(nameof(MongoIntegrationFixture))]
public class MongoOwnerUserIdIsolationTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ListAsync под user-1 не видит intents user-2")]
    public async Task List_filters_by_owner_user_id()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var uow = new MongoUnitOfWork(fixture.Client, sessions);

        var asUser1 = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor("user-1"));
        var asUser2 = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor("user-2"));

        var u1 = Intent.Create(IntentId.New(), "user-1", "u1 intent", null, Now);
        var u2 = Intent.Create(IntentId.New(), "user-2", "u2 intent", null, Now);

        await uow.ExecuteAsync(ct => asUser1.CreateAsync(
            u1, Snapshot(u1), Initial(u1), Array.Empty<Throne.Domain.Tags.Tag>(), ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => asUser2.CreateAsync(
            u2, Snapshot(u2), Initial(u2), Array.Empty<Throne.Domain.Tags.Tag>(), ct), CancellationToken.None);

        var listAsU1 = await asUser1.ListAsync(statuses: null, CancellationToken.None);
        var listAsU2 = await asUser2.ListAsync(statuses: null, CancellationToken.None);

        listAsU1.Select(i => i.Id.Value).Should().BeEquivalentTo([u1.Id.Value]);
        listAsU2.Select(i => i.Id.Value).Should().BeEquivalentTo([u2.Id.Value]);
    }

    [Fact(DisplayName = "GetByIdAsync под user-1 не возвращает intent user-2")]
    public async Task GetById_filters_by_owner_user_id()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var uow = new MongoUnitOfWork(fixture.Client, sessions);

        var asUser2 = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor("user-2"));
        var asUser1 = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor("user-1"));

        var u2 = Intent.Create(IntentId.New(), "user-2", "secret", null, Now);
        await uow.ExecuteAsync(ct => asUser2.CreateAsync(
            u2, Snapshot(u2), Initial(u2), Array.Empty<Throne.Domain.Tags.Tag>(), ct), CancellationToken.None);

        var crossRead = await asUser1.GetByIdAsync(u2.Id, CancellationToken.None);
        crossRead.Should().BeNull("user-1 must not see user-2's intent");

        var ownRead = await asUser2.GetByIdAsync(u2.Id, CancellationToken.None);
        ownRead.Should().NotBeNull();
    }

    [Fact(DisplayName = "DeleteAsync под user-1 не удаляет intent user-2")]
    public async Task Delete_does_not_cross_users()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var uow = new MongoUnitOfWork(fixture.Client, sessions);

        var asUser2 = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor("user-2"));
        var asUser1 = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor("user-1"));

        var u2 = Intent.Create(IntentId.New(), "user-2", "owned by u2", null, Now);
        await uow.ExecuteAsync(ct => asUser2.CreateAsync(
            u2, Snapshot(u2), Initial(u2), Array.Empty<Throne.Domain.Tags.Tag>(), ct), CancellationToken.None);

        var crossDelete = await uow.ExecuteAsync(ct => asUser1.DeleteAsync(u2.Id, ct), CancellationToken.None);
        crossDelete.Should().BeOfType<DeleteIntentOutcome.NotFound>("delete must not cross owners");

        var stillThere = await asUser2.GetByIdAsync(u2.Id, CancellationToken.None);
        stillThere.Should().NotBeNull();
    }

    private static TextVersion Snapshot(Intent intent) => TextVersion.CreateSnapshot(
        Guid.NewGuid().ToString("N"),
        TextVersionOwnerKind.Intent,
        intent.Id.Value,
        intent.Text,
        Now,
        TextVersionAuthor.Agent);

    private static IntentStatusChange Initial(Intent intent) => IntentStatusChange.Create(
        Guid.NewGuid().ToString("N"),
        intent.Id,
        intent.CurrentVersion,
        intent.Status,
        intent.Status,
        "test:create",
        Now,
        IntentTrainingAuthor.Agent);
}
