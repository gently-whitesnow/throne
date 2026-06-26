using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Rows;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentRepositoryTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет Intent в intents и v1 snapshot в intent_events")]
    public async Task Create_persists_canonical_and_v1_snapshot()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var tagId = TagId.New();
        var intent = Intent.Create(id, "hello world", [tagId], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "hello world", Now, TextVersionAuthor.Agent);

        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var stored = await FindIntentAsync(db, id);
        stored.Should().NotBeNull();
        stored!.Text.Should().Be("hello world");
        stored.Status.Should().Be(IntentStatusNames.Draft);
        stored.CurrentVersion.Should().Be(1);
        stored.TagIds.Should().Equal(tagId.Value);

        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(1);
        events[0].Version.Should().Be(1);
        events[0].Kind.Should().Be("text_changed");
        events[0].TextChange!.Kind.Should().Be("create");
        events[0].TextChange!.Snapshot.Should().Be("hello world");
    }

    [Fact(DisplayName = "GetByIdAsync читает Intent из intents в доменной форме")]
    public async Task Get_returns_persisted_intent()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var a = TagId.New();
        var b = TagId.New();
        var intent = Intent.Create(id, "body", [a, b], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var fetched = await repo.GetByIdAsync(id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.State.Text.Should().Be("body");
        fetched.State.Status.Should().Be(IntentStatusNames.Draft);
        fetched.State.CurrentVersion.Should().Be(1);
        fetched.TagIds.Should().Equal(a, b);
    }

    [Fact(DisplayName = "GetByIdAsync возвращает null для несуществующего id")]
    public async Task Get_returns_null_when_missing()
    {
        var (_, repo, _) = await NewScopeAsync();

        var fetched = await repo.GetByIdAsync(new IntentId("nope"), CancellationToken.None);

        fetched.Should().BeNull();
    }

    [Fact(DisplayName = "SetStatusAsync обновляет статус и пишет status-change log")]
    public async Task SetStatus_updates_status_and_writes_log()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "body", [TagId.New()], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var outcome = await uow.ExecuteAsync(
            ct => repo.SetStatusAsync(
                id,
                IntentStatusNames.Work,
                appendText: null,
                reason: null,
                IntentTrainingAuthor.System,
                "set_intent_status:work",
                Now.AddMinutes(5),
                ct),
            CancellationToken.None);

        var updated = outcome.Should().BeOfType<SetIntentStatusOutcome.Updated>().Subject.Intent;
        updated.State.Status.Should().Be(IntentStatusNames.Work);
        updated.State.CurrentVersion.Should().Be(1);

        var stored = await FindIntentAsync(db, id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(IntentStatusNames.Work);

        var changes = await ListStatusChangesAsync(db, id);
        changes.Should().HaveCount(2);
        changes[1].FromStatus.Should().Be(IntentStatusNames.Draft);
        changes[1].ToStatus.Should().Be(IntentStatusNames.Work);
        changes[1].Source.Should().Be("set_intent_status:work");
        changes[1].CreatedBy.Should().Be("system");
        changes[1].Reason.Should().BeNull();
    }

    [Fact(DisplayName = "SetStatusAsync с reason для не-reject перехода сохраняет reason в логе и не трогает Intent.text")]
    public async Task SetStatus_persists_reason_for_non_reject_transition()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "body", [TagId.New()], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        await uow.ExecuteAsync(
            ct => repo.SetStatusAsync(
                id,
                IntentStatusNames.AwaitingOperator,
                appendText: null,
                reason: "нужен доступ к prod",
                IntentTrainingAuthor.Agent,
                "set_intent_status",
                Now.AddMinutes(5),
                ct),
            CancellationToken.None);

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("body");

        var changes = await ListStatusChangesAsync(db, id);
        changes.Should().HaveCount(2);
        changes[1].ToStatus.Should().Be(IntentStatusNames.AwaitingOperator);
        changes[1].Reason.Should().Be("нужен доступ к prod");
    }

    [Fact(DisplayName = "SetCleanupLocalStateOnDoneAsync переключает флаг без бампа версии и читается обратно")]
    public async Task SetCleanupFlag_persists_without_version_bump()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "body", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var created = await repo.GetByIdAsync(id, CancellationToken.None);
        created!.State.CleanupLocalStateOnClose.Should().BeTrue("по умолчанию очистка включена");

        await uow.ExecuteAsync(
            ct => repo.SetCleanupLocalStateOnDoneAsync(id, false, Now.AddMinutes(1), ct),
            CancellationToken.None);

        var fetched = await repo.GetByIdAsync(id, CancellationToken.None);
        fetched!.State.CleanupLocalStateOnClose.Should().BeFalse();
        fetched.State.CurrentVersion.Should().Be(1);

        var stored = await FindIntentAsync(db, id);
        stored!.CleanupLocalStateOnDone.Should().BeFalse();
    }

    [Fact(DisplayName = "CreateAsync вне UoW бросает InvalidOperationException")]
    public async Task Create_without_uow_throws()
    {
        var (_, repo, _) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "x", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "x", Now, TextVersionAuthor.Agent);

        var act = () => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task<(SqliteTestDatabase Db, IIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<IIntentRepository>(), db.GetRequiredService<IUnitOfWork>());
    }

    private static async Task<IntentRow?> FindIntentAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentRow>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value);
    }

    private static async Task<List<IntentEventRow>> ListEventsAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentEventRow>().AsNoTracking()
            .Where(x => x.IntentId == id.Value)
            .OrderBy(x => x.Version)
            .ToListAsync();
    }

    private static async Task<List<IntentStatusChangeRow>> ListStatusChangesAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentStatusChangeRow>().AsNoTracking()
            .Where(x => x.IntentId == id.Value)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.State.CurrentVersion,
            intent.State.Status,
            intent.State.Status,
            "test:create",
            Now,
            IntentTrainingAuthor.Agent);
}
