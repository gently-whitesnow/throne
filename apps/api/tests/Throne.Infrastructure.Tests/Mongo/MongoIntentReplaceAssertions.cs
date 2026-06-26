using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.Mongo;

internal static class MongoIntentReplaceAssertions
{
    public static void AssertIntentState(Intent intent, string text, int version)
    {
        intent.State.Text.Should().Be(text);
        intent.State.CurrentVersion.Should().Be(version);
    }

    public static async Task AssertStoredAsync(SqliteTestDatabase db, IntentId id, string text, int version)
    {
        await using var ctx = await db.CreateContextAsync();
        var stored = await ctx.Set<IntentRow>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id.Value);
        stored!.Text.Should().Be(text);
        stored.CurrentVersion.Should().Be(version);
    }

    public static async Task AssertReplaceEventAsync(SqliteTestDatabase db, IntentId id, string oldText, string newText)
    {
        await using var ctx = await db.CreateContextAsync();
        var events = await ctx.Set<IntentEventRow>().AsNoTracking()
            .Where(d => d.IntentId == id.Value)
            .OrderBy(d => d.Version)
            .ToListAsync();
        events.Should().HaveCount(2);
        events[1].Version.Should().Be(2);
        events[1].Kind.Should().Be("text_changed");
        events[1].TextChange!.Kind.Should().Be("replace");
        events[1].TextChange!.OldText.Should().Be(oldText);
        events[1].TextChange!.NewText.Should().Be(newText);
        events[1].TextChange!.Snapshot.Should().BeNull();
    }
}
