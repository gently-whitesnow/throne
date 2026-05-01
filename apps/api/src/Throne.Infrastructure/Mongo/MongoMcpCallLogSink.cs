using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoMcpCallLogSink(IMongoDatabase database) : IMcpCallLogSink
{
    private readonly IMongoCollection<McpCallLogDocument> _collection =
        database.GetCollection<McpCallLogDocument>(MongoCollectionNames.McpCallLog);

    public Task WriteAsync(McpCallLogEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var doc = new McpCallLogDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = entry.CreatedAt.UtcDateTime,
            SessionId = entry.SessionId,
            ToolName = entry.ToolName,
            Arguments = ToBson(entry.Arguments),
            IntentId = entry.IntentId,
            ModeHint = entry.ModeHint,
            Outcome = entry.Outcome.ToWire(),
            ErrorCode = entry.ErrorCode,
            ResultSummary = entry.ResultSummary is null ? null : ToBson(entry.ResultSummary),
            DurationMs = entry.DurationMs,
            ServerVersion = entry.ServerVersion,
        };

        return _collection.InsertOneAsync(doc, options: null, ct);
    }

    private static BsonDocument ToBson(IReadOnlyDictionary<string, object?> source)
    {
        var doc = new BsonDocument();
        foreach (var (key, value) in source)
        {
            doc[key] = value is null ? BsonNull.Value : BsonValue.Create(value);
        }

        return doc;
    }
}
