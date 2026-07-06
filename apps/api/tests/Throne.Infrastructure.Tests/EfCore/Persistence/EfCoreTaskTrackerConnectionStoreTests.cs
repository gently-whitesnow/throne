using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
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

    [Fact(DisplayName = "A freshly saved connection has no observed health yet (LastStatus == null baseline)")]
    public async Task NewConnectionHasNoHealth()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();
        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);

        var stored = await store.GetAsync("kaiten", CancellationToken.None);

        stored!.LastStatus.Should().BeNull();
        stored.LastError.Should().BeNull();
        stored.LastCheckedAt.Should().BeNull();
    }

    [Fact(DisplayName = "SaveHealth persists the status, detail and checked-at against the saved connection")]
    public async Task SaveHealthPersistsStatus()
    {
        var checkedAt = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();
        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);

        await store.SaveHealthAsync(
            "kaiten", TaskTrackerConnectionHealth.Auth, "token rejected", checkedAt, CancellationToken.None);

        var stored = await store.GetAsync("kaiten", CancellationToken.None);
        stored!.LastStatus.Should().Be(TaskTrackerConnectionHealth.Auth);
        stored.LastError.Should().Be("token rejected");
        stored.LastCheckedAt.Should().BeCloseTo(checkedAt, TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "SaveHealth(Connected) clears the stored error detail")]
    public async Task SaveHealthConnectedClearsError()
    {
        var checkedAt = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();
        await store.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);
        await store.SaveHealthAsync(
            "kaiten", TaskTrackerConnectionHealth.Auth, "token rejected", checkedAt, CancellationToken.None);

        await store.SaveHealthAsync(
            "kaiten", TaskTrackerConnectionHealth.Connected, "ignored", checkedAt, CancellationToken.None);

        var stored = await store.GetAsync("kaiten", CancellationToken.None);
        stored!.LastStatus.Should().Be(TaskTrackerConnectionHealth.Connected);
        stored.LastError.Should().BeNull();
    }

    [Fact(DisplayName = "SaveHealth on an absent connection is a no-op (health belongs to a saved connection)")]
    public async Task SaveHealthOnMissingConnectionIsNoop()
    {
        var checkedAt = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);
        await using var db = await sqlite.CreateDatabaseAsync();
        var store = db.GetRequiredService<ITaskTrackerConnectionStore>();

        var act = () => store.SaveHealthAsync(
            "kaiten", TaskTrackerConnectionHealth.Offline, "down", checkedAt, CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await store.GetAsync("kaiten", CancellationToken.None)).Should().BeNull();
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
