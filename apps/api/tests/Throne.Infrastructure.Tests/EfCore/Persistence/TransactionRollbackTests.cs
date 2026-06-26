using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class TransactionRollbackTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ExecuteAsync коммитит обе записи, если лямбда завершилась без ошибки")]
    public async Task Commits_both_writes_on_success()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var id = IntentId.New();
        var intent = Intent.Create(id, "ok", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "ok", Now, TextVersionAuthor.Agent);

        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);

        (await IntentExistsAsync(db, id)).Should().BeTrue();
        (await EventExistsAsync(db, id)).Should().BeTrue();
    }

    [Fact(DisplayName = "ExecuteAsync откатывает первую запись, если вторая бросает")]
    public async Task Rolls_back_first_write_on_failure()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var id = IntentId.New();
        var intent = Intent.Create(id, "boom", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "boom", Now, TextVersionAuthor.Agent);

        var act = async () => await uow.ExecuteAsync(async ct =>
        {
            await repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct);
            throw new InvalidOperationException("boom");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        (await IntentExistsAsync(db, id)).Should().BeFalse();
        (await EventExistsAsync(db, id)).Should().BeFalse();
    }

    private async Task<(SqliteTestDatabase Db, IIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<IIntentRepository>(), db.GetRequiredService<IUnitOfWork>());
    }

    private static async Task<bool> IntentExistsAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentRow>().AsNoTracking().AnyAsync(x => x.Id == id.Value);
    }

    private static async Task<bool> EventExistsAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentEventRow>().AsNoTracking().AnyAsync(x => x.IntentId == id.Value);
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
