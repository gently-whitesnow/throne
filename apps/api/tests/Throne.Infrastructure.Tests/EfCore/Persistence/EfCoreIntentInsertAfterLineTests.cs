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
public class EfCoreIntentInsertAfterLineTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "InsertTextAfterLineAsync атомарно обновляет intents и пишет v2 в intent_events")]
    public async Task Insert_writes_v2()
    {
        var (db, repo, uow, id) = await SeedAsync("a\nb");

        var outcome = await uow.ExecuteAsync(
            ct => repo.InsertTextAfterLineAsync(id, expectedVersion: 1, afterLine: 1, "X\n", Edited, ct),
            CancellationToken.None);

        var inserted = outcome.Should().BeOfType<InsertIntentTextAfterLineOutcome.Inserted>().Subject;
        inserted.Intent.State.Text.Should().Be("a\nX\nb");
        inserted.Intent.State.CurrentVersion.Should().Be(2);

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("a\nX\nb");
        stored.CurrentVersion.Should().Be(2);

        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(2);
        events[1].Kind.Should().Be("text_changed");
        events[1].TextChange!.Kind.Should().Be("insert");
        events[1].TextChange!.AfterLine.Should().Be(1);
        events[1].TextChange!.InsertText.Should().Be("X\n");
    }

    [Fact(DisplayName = "InsertTextAfterLineAsync с неверным expected_version возвращает VersionConflict без side-effects")]
    public async Task Wrong_expected_version_returns_conflict()
    {
        var (db, repo, uow, id) = await SeedAsync("a");

        var outcome = await uow.ExecuteAsync(
            ct => repo.InsertTextAfterLineAsync(id, expectedVersion: 99, afterLine: 0, "x", Edited, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<InsertIntentTextAfterLineOutcome.VersionConflict>();

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("a");
        stored.CurrentVersion.Should().Be(1);
        var events = await ListEventsAsync(db, id);
        events.Should().HaveCount(1);
    }

    [Fact(DisplayName = "InsertTextAfterLineAsync с after_line вне диапазона возвращает LineOutOfRange")]
    public async Task Out_of_range_returns_line_out_of_range()
    {
        var (db, repo, uow, id) = await SeedAsync("a\nb");

        var outcome = await uow.ExecuteAsync(
            ct => repo.InsertTextAfterLineAsync(id, expectedVersion: 1, afterLine: 99, "x", Edited, ct),
            CancellationToken.None);

        var oor = outcome.Should().BeOfType<InsertIntentTextAfterLineOutcome.LineOutOfRange>().Subject;
        oor.TotalLines.Should().Be(2);
        oor.RequestedAfterLine.Should().Be(99);

        var stored = await FindIntentAsync(db, id);
        stored!.Text.Should().Be("a\nb");
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
