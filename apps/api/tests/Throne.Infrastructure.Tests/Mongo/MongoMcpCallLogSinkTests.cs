using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
public class MongoMcpCallLogSinkTests(MongoFixture fixture)
{
    [Fact(DisplayName = "WriteAsync пишет audit-запись со всеми полями в коллекцию mcp_call_log")]
    public async Task WriteAsync_persists_entry()
    {
        var dbName = $"throne_audit_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(dbName);
        var db = fixture.Client.GetDatabase(dbName);
        IMcpCallLogSink sink = new MongoMcpCallLogSink(db);

        var entry = new McpCallLogEntry(
            CreatedAt: new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            SessionId: "session-1",
            ToolName: "create_intent",
            Arguments: new Dictionary<string, object?> { ["text"] = "hello", ["tags"] = "[\"throne\"]" },
            IntentId: null,
            ModeHint: null,
            Outcome: McpCallOutcome.Success,
            ErrorCode: null,
            ResultSummary: new Dictionary<string, object?> { ["intent_id"] = "abc", ["current_version"] = 1 },
            DurationMs: 17,
            ServerVersion: "0.1.0");

        await sink.WriteAsync(entry, CancellationToken.None);

        var stored = await db.GetCollection<McpCallLogDocument>(MongoCollectionNames.McpCallLog)
            .Find(_ => true).ToListAsync();

        stored.Should().HaveCount(1);
        var doc = stored[0];
        doc.ToolName.Should().Be("create_intent");
        doc.Outcome.Should().Be("success");
        doc.SessionId.Should().Be("session-1");
        doc.DurationMs.Should().Be(17);
        doc.ServerVersion.Should().Be("0.1.0");
        doc.Arguments.GetValue("text").AsString.Should().Be("hello");
        doc.ResultSummary.Should().NotBeNull();
        doc.ResultSummary!.GetValue("intent_id").AsString.Should().Be("abc");
    }
}
