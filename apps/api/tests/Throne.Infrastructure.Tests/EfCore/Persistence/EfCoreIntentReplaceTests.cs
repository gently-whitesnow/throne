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
public class EfCoreIntentReplaceTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ReplaceTextAsync обновляет intents и пишет v2 в text_versions атомарно")]
    public async Task Replace_updates_intent_and_writes_v2_version()
    {
        var (db, repo, uow, id) = await SeedAsync("hello world");

        var outcome = await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(id, expectedVersion: 1, "world", "there", TextVersionAuthor.Agent, Edited, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<ReplaceIntentTextOutcome.Replaced>();
        var intent = ((ReplaceIntentTextOutcome.Replaced)outcome).Intent;
        EfCoreIntentReplaceAssertions.AssertIntentState(intent, "hello there", 2);

        await EfCoreIntentReplaceAssertions.AssertStoredAsync(db, id, "hello there", 2);
        await EfCoreIntentReplaceAssertions.AssertReplaceEventAsync(db, id, "world", "there");
    }

    [Fact(DisplayName = "ReplaceTextAsync с неверным expected_version возвращает VersionConflict без side-effects")]
    public async Task Wrong_expected_version_returns_conflict()
    {
        var (db, repo, uow, id) = await SeedAsync("hello world");

        var outcome = await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(id, expectedVersion: 99, "world", "there", TextVersionAuthor.Agent, Edited, ct),
            CancellationToken.None);

        var conflict = outcome.Should().BeOfType<ReplaceIntentTextOutcome.VersionConflict>().Subject;
        conflict.CurrentVersion.Should().Be(1);

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("hello world");
        stored.CurrentVersion.Should().Be(1);

        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(1);
    }

    [Fact(DisplayName = "ReplaceTextAsync с MatchNotFound не пишет ничего в text_versions")]
    public async Task Match_not_found_writes_nothing()
    {
        var (db, repo, uow, id) = await SeedAsync("hello world");

        var outcome = await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(id, expectedVersion: 1, "missing", "x", TextVersionAuthor.Agent, Edited, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<ReplaceIntentTextOutcome.MatchNotFound>();

        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(1);
        var stored = await FindIntentAsync(db, id);
        stored!.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "ReplaceTextAsync с MatchAmbiguous не пишет ничего")]
    public async Task Match_ambiguous_writes_nothing()
    {
        var (db, repo, uow, id) = await SeedAsync("foo\nfoo\nfoo");

        var outcome = await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(id, expectedVersion: 1, "foo", "bar", TextVersionAuthor.Agent, Edited, ct),
            CancellationToken.None);

        var ambiguous = outcome.Should().BeOfType<ReplaceIntentTextOutcome.MatchAmbiguous>().Subject;
        ambiguous.MatchesCount.Should().Be(3);

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("foo\nfoo\nfoo");
        stored.CurrentVersion.Should().Be(1);
        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(1);
    }

    [Fact(DisplayName = "ReplaceTextAsync на несуществующем Intent возвращает NotFound")]
    public async Task Missing_intent_returns_not_found()
    {
        var (_, repo, uow, _) = await SeedAsync("seed");

        var outcome = await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(new IntentId("nope"), 1, "x", "y", TextVersionAuthor.Agent, Edited, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<ReplaceIntentTextOutcome.NotFound>();
    }

    [Fact(DisplayName = "ReplaceTextAsync вне UoW бросает InvalidOperationException")]
    public async Task Replace_without_uow_throws()
    {
        var (_, repo, _, id) = await SeedAsync("hello");

        var act = () => repo.ReplaceTextAsync(id, 1, "hello", "world", TextVersionAuthor.Agent, Edited, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Транзакция откатывает Replaced-write, если вызывающий код бросает")]
    public async Task Replace_rolls_back_when_caller_throws()
    {
        var (db, repo, uow, id) = await SeedAsync("hello world");

        var act = async () => await uow.ExecuteAsync(async ct =>
        {
            await repo.ReplaceTextAsync(id, 1, "world", "there", TextVersionAuthor.Agent, Edited, ct);
            throw new InvalidOperationException("boom");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("hello world");
        stored.CurrentVersion.Should().Be(1);
        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(1);
    }

    private async Task<(SqliteTestDatabase Db, IIntentRepository Repo, IUnitOfWork Uow, IntentId Id)> SeedAsync(string text)
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        var id = IntentId.New();
        var intent = Intent.Create(id, text, null, Created);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, text, Created, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);
        return (db, repo, uow, id);
    }

    private static async Task<IntentRow?> FindIntentAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentRow>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id.Value);
    }

    private static async Task<List<IntentEventRow>> ListEventsAsync(SqliteTestDatabase db, IntentId id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentEventRow>().AsNoTracking()
            .Where(d => d.IntentId == id.Value)
            .OrderBy(d => d.Version)
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
            Created,
            IntentTrainingAuthor.Agent);
}
