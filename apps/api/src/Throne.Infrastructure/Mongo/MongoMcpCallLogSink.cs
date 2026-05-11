using System.Text.Json;
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
            UserId = entry.UserId,
            ToolName = entry.ToolName,
            Arguments = ToBson(entry.Arguments),
            IntentId = entry.IntentId,
            ModeHint = entry.ModeHint,
            Outcome = entry.Outcome.ToWire(),
            ErrorCode = entry.ErrorCode,
            ErrorMessage = entry.ErrorMessage,
            ExceptionType = entry.ExceptionType,
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
            doc[key] = ToBsonValue(value);
        }

        return doc;
    }

    private static BsonValue ToBsonValue(object? value) => value switch
    {
        null => BsonNull.Value,
        JsonElement element => JsonElementToBson(element),
        IReadOnlyDictionary<string, object?> dict => ToBson(dict),
        IDictionary<string, object?> dict => ToBsonDictionary(dict),
        IEnumerable<object?> list when value is not string => new BsonArray(list.Select(ToBsonValue)),
        _ => BsonValue.Create(value),
    };

    private static BsonDocument ToBsonDictionary(IDictionary<string, object?> source)
    {
        var doc = new BsonDocument();
        foreach (var (key, value) in source)
        {
            doc[key] = ToBsonValue(value);
        }

        return doc;
    }

    private static BsonValue JsonElementToBson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => JsonObjectToBson(element),
        JsonValueKind.Array => new BsonArray(element.EnumerateArray().Select(JsonElementToBson)),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var i) ? new BsonInt64(i) : new BsonDouble(element.GetDouble()),
        JsonValueKind.True => BsonBoolean.True,
        JsonValueKind.False => BsonBoolean.False,
        JsonValueKind.Null => BsonNull.Value,
        _ => BsonNull.Value,
    };

    private static BsonDocument JsonObjectToBson(JsonElement element)
    {
        var doc = new BsonDocument();
        foreach (var property in element.EnumerateObject())
        {
            doc[property.Name] = JsonElementToBson(property.Value);
        }

        return doc;
    }
}
