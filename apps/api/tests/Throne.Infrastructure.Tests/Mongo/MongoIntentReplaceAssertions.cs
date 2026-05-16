using FluentAssertions;
using MongoDB.Driver;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

internal static class MongoIntentReplaceAssertions
{
    public static void AssertIntentState(Intent intent, string text, int version)
    {
        intent.State.Text.Should().Be(text);
        intent.State.CurrentVersion.Should().Be(version);
    }

    public static async Task AssertStoredAsync(IMongoDatabase db, IntentId id, string text, int version)
    {
        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(d => d.Id == id.Value).FirstOrDefaultAsync();
        stored!.Text.Should().Be(text);
        stored.CurrentVersion.Should().Be(version);
    }

    public static async Task AssertReplaceEventAsync(IMongoDatabase db, IntentId id, string oldText, string newText)
    {
        var events = await db.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents)
            .Find(d => d.IntentId == id.Value).SortBy(d => d.Version).ToListAsync();
        events.Should().HaveCount(2);
        events[1].Version.Should().Be(2);
        events[1].Kind.Should().Be("text_changed");
        events[1].TextChange!.Kind.Should().Be("replace");
        events[1].TextChange!.OldText.Should().Be(oldText);
        events[1].TextChange!.NewText.Should().Be(newText);
        events[1].TextChange!.Snapshot.Should().BeNull();
    }
}
