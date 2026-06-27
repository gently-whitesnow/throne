using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class EfCoreTaskTrackerConnectionStoreTests(SqliteFixture sqlite)
{
    [Fact(DisplayName = "Save then Get round-trips the base URL and token (stored as-is, local-first)")]
    public async Task SavesAndReadsBack()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();

        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok-secret-123", CancellationToken.None);

        var stored = await store.GetAsync("kaiten", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.BaseUrl.Should().Be("https://acme.kaiten.ru");
        stored.Token.Should().Be("tok-secret-123");

        await using var context = await db.CreateContextAsync();
        var row = await context.Set<TaskTrackerConnectionRow>().AsNoTracking()
            .SingleAsync(r => r.Tracker == "kaiten");
        row.Token.Should().Be("tok-secret-123");
    }

    [Fact(DisplayName = "Re-saving the connection preserves the existing board selection")]
    public async Task ReconnectKeepsSelection()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();
        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);
        await store.SaveSelectionAsync(
            "kaiten",
            [new TaskTrackerBoardSelection("1", "Space", "10", "Board", "lane")],
            CancellationToken.None);

        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok-rotated", CancellationToken.None);

        var stored = await store.GetAsync("kaiten", CancellationToken.None);
        stored!.Token.Should().Be("tok-rotated");
        stored.Selection.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new TaskTrackerBoardSelection("1", "Space", "10", "Board", "lane"));
    }

    [Fact(DisplayName = "Delete drops the connection and its selection")]
    public async Task DeleteRemovesEverything()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();
        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);

        await store.DeleteAsync("kaiten", CancellationToken.None);

        (await store.GetAsync("kaiten", CancellationToken.None)).Should().BeNull();
    }
}
