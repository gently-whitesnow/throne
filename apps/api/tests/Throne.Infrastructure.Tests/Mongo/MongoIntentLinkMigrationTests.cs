using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentLinkMigrationTests(MongoFixture fixture)
{
    [Fact(DisplayName = "миграция схлопывает links до from/to + blocking и разворачивает derived_from")]
    public async Task Migrates_links_and_events_to_blocking_edges()
    {
        var dbName = $"throne_link_migration_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(dbName);
        var db = fixture.Client.GetDatabase(dbName);
        var links = db.GetCollection<BsonDocument>(MongoCollectionNames.IntentLinks);
        var events = db.GetCollection<BsonDocument>(MongoCollectionNames.IntentEvents);

        await links.InsertManyAsync(
            [
                Link("block", "parent", "child", "blocks"),
                Link("derived-collides", "child", "parent", "derived_from"),
                Link("relates", "a", "b", "relates"),
                Link("duplicate", "c", "d", "duplicate_of"),
            ]);
        await events.InsertManyAsync(
            [
                LinkEvent("event-derived", "child", "parent", "derived_from"),
                LinkEvent("event-block", "parent", "child", "blocks"),
            ]);

        await MongoIntentLinkMigration.RunAsync(db, CancellationToken.None);
        await MongoIntentLinkMigration.RunAsync(db, CancellationToken.None);

        var migratedLinks = await links.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();
        migratedLinks.Should().ContainSingle();
        var link = migratedLinks[0];
        link["_id"].AsString.Should().Be("block");
        link["from_id"].AsString.Should().Be("parent");
        link["to_id"].AsString.Should().Be("child");
        link["blocking"].AsBoolean.Should().BeTrue();
        link.Contains("type").Should().BeFalse();

        var derivedEvent = await events.Find(Builders<BsonDocument>.Filter.Eq("_id", "event-derived")).SingleAsync();
        derivedEvent["intent_id"].AsString.Should().Be("parent");
        derivedEvent["peer_intent_id"].AsString.Should().Be("child");
        derivedEvent["link"].AsBsonDocument["from_id"].AsString.Should().Be("parent");
        derivedEvent["link"].AsBsonDocument["to_id"].AsString.Should().Be("child");
        derivedEvent["link"].AsBsonDocument["blocking"].AsBoolean.Should().BeFalse();
        derivedEvent["link"].AsBsonDocument.Contains("type").Should().BeFalse();

        var blockEvent = await events.Find(Builders<BsonDocument>.Filter.Eq("_id", "event-block")).SingleAsync();
        blockEvent["link"].AsBsonDocument["blocking"].AsBoolean.Should().BeTrue();
        blockEvent["link"].AsBsonDocument.Contains("type").Should().BeFalse();
    }

    private static BsonDocument Link(string id, string from, string to, string type) => new()
    {
        ["_id"] = id,
        ["from_id"] = from,
        ["to_id"] = to,
        ["type"] = type,
        ["author"] = "agent",
        ["created_at"] = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
    };

    private static BsonDocument LinkEvent(string id, string from, string to, string type) => new()
    {
        ["_id"] = id,
        ["intent_id"] = from,
        ["peer_intent_id"] = to,
        ["kind"] = "link_added",
        ["link"] = Link($"link-{id}", from, to, type),
        ["created_at"] = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
    };
}
