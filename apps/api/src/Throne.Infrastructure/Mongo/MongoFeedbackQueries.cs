using System.Globalization;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoFeedbackQueries(IMongoDatabase database) : IFeedbackQueries
{
    private readonly IMongoCollection<IntentQaDocument> _qa =
        database.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa);

    private readonly IMongoCollection<IntentReviewDocument> _reviews =
        database.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview);

    private readonly IMongoCollection<McpCallLogDocument> _mcp =
        database.GetCollection<McpCallLogDocument>(MongoCollectionNames.McpCallLog);

    public async Task<FeedbackPage<IntentQa>> ListQaAsync(FeedbackListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = new List<FilterDefinition<IntentQaDocument>>
        {
            Builders<IntentQaDocument>.Filter.Gte(d => d.CreatedAt, query.Since.UtcDateTime),
        };
        if (query.IntentId is not null)
        {
            filters.Add(Builders<IntentQaDocument>.Filter.Eq(d => d.IntentId, query.IntentId));
        }
        ApplyCursor(filters, query.Cursor, d => d.CreatedAt, d => d.Id);

        var page = await _qa
            .Find(Builders<IntentQaDocument>.Filter.And(filters))
            .SortBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Limit(query.Limit + 1)
            .ToListAsync(ct).ConfigureAwait(false);

        var (slice, next) = TakePage(page, query.Limit, d => d.CreatedAt, d => d.Id);
        return new FeedbackPage<IntentQa>(
            slice.Select(MapQa).ToArray(),
            next);
    }

    public async Task<FeedbackPage<IntentReview>> ListReviewsAsync(ReviewListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = new List<FilterDefinition<IntentReviewDocument>>
        {
            Builders<IntentReviewDocument>.Filter.Gte(d => d.CreatedAt, query.Since.UtcDateTime),
        };
        if (query.IntentId is not null)
        {
            filters.Add(Builders<IntentReviewDocument>.Filter.Eq(d => d.IntentId, query.IntentId));
        }
        if (query.ReasonFilter is not null)
        {
            filters.Add(Builders<IntentReviewDocument>.Filter.Regex(
                d => d.Reason,
                new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.ReasonFilter), "i")));
        }
        ApplyCursor(filters, query.Cursor, d => d.CreatedAt, d => d.Id);

        var page = await _reviews
            .Find(Builders<IntentReviewDocument>.Filter.And(filters))
            .SortBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Limit(query.Limit + 1)
            .ToListAsync(ct).ConfigureAwait(false);

        var (slice, next) = TakePage(page, query.Limit, d => d.CreatedAt, d => d.Id);
        return new FeedbackPage<IntentReview>(
            slice.Select(MapReview).ToArray(),
            next);
    }

    public async Task<FeedbackPage<McpCallLogRecord>> QueryMcpCallLogAsync(McpCallLogQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = new List<FilterDefinition<McpCallLogDocument>>
        {
            Builders<McpCallLogDocument>.Filter.Gte(d => d.CreatedAt, query.Since.UtcDateTime),
        };
        if (query.ToolName is not null)
        {
            filters.Add(Builders<McpCallLogDocument>.Filter.Eq(d => d.ToolName, query.ToolName));
        }
        if (query.OutcomeFilter is not null)
        {
            filters.Add(Builders<McpCallLogDocument>.Filter.Eq(d => d.Outcome, query.OutcomeFilter));
        }
        if (query.IntentId is not null)
        {
            filters.Add(Builders<McpCallLogDocument>.Filter.Eq(d => d.IntentId, query.IntentId));
        }
        if (query.SessionId is not null)
        {
            filters.Add(Builders<McpCallLogDocument>.Filter.Eq(d => d.SessionId, query.SessionId));
        }
        ApplyCursor(filters, query.Cursor, d => d.CreatedAt, d => d.Id);

        var page = await _mcp
            .Find(Builders<McpCallLogDocument>.Filter.And(filters))
            .SortBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Limit(query.Limit + 1)
            .ToListAsync(ct).ConfigureAwait(false);

        var (slice, next) = TakePage(page, query.Limit, d => d.CreatedAt, d => d.Id);
        return new FeedbackPage<McpCallLogRecord>(
            slice.Select(MapMcpCall).ToArray(),
            next);
    }

    private static (IReadOnlyList<T> Slice, string? NextCursor) TakePage<T>(
        List<T> page,
        int limit,
        Func<T, DateTime> getCreatedAt,
        Func<T, string> getId)
    {
        if (page.Count <= limit)
        {
            return (page, null);
        }

        var slice = page.GetRange(0, limit);
        var last = slice[^1];
        return (slice, EncodeCursor(getCreatedAt(last), getId(last)));
    }

    private static void ApplyCursor<T>(
        List<FilterDefinition<T>> filters,
        string? cursor,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdAtSelector,
        System.Linq.Expressions.Expression<Func<T, string>> idSelector)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return;
        }

        var (createdAt, id) = DecodeCursor(cursor);

        // (created_at, id) > (cursor.created_at, cursor.id) — strict tuple comparison.
        var b = Builders<T>.Filter;
        filters.Add(b.Or(
            b.Gt(createdAtSelector, createdAt),
            b.And(
                b.Eq(createdAtSelector, createdAt),
                b.Gt(idSelector, id))));
    }

    public static string EncodeCursor(DateTime createdAtUtc, string id)
    {
        var iso = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
            .ToString("o", CultureInfo.InvariantCulture);
        var json = JsonSerializer.Serialize(new CursorPayload(iso, id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static (DateTime CreatedAtUtc, string Id) DecodeCursor(string cursor)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var json = Encoding.UTF8.GetString(bytes);
            var payload = JsonSerializer.Deserialize<CursorPayload>(json)
                ?? throw new FormatException("cursor payload is null.");
            var parsed = DateTime.Parse(payload.created_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return (DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc), payload.id);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "cursor is not a valid pagination cursor.",
                new Dictionary<string, object?> { ["cursor"] = cursor });
        }
    }

    private sealed record CursorPayload(string created_at, string id);

    private static IntentQa MapQa(IntentQaDocument doc) => new(
        Id: doc.Id,
        IntentId: new IntentId(doc.IntentId),
        IntentVersionAtWrite: doc.IntentVersionAtWrite,
        Question: doc.Question,
        Answer: doc.Answer,
        CreatedAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        CreatedBy: MongoEnumNames.ParseIntentTrainingAuthor(doc.CreatedBy));

    private static IntentReview MapReview(IntentReviewDocument doc) => new(
        Id: doc.Id,
        IntentId: new IntentId(doc.IntentId),
        IntentVersionAtWrite: doc.IntentVersionAtWrite,
        Note: doc.Note,
        Reason: doc.Reason,
        CreatedAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        CreatedBy: MongoEnumNames.ParseIntentTrainingAuthor(doc.CreatedBy));

    private static McpCallLogRecord MapMcpCall(McpCallLogDocument doc) => new(
        Id: doc.Id,
        CreatedAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        ToolName: doc.ToolName,
        Outcome: doc.Outcome,
        ErrorCode: doc.ErrorCode,
        DurationMs: doc.DurationMs,
        IntentId: doc.IntentId,
        SessionId: doc.SessionId,
        ArgumentSummary: McpArgumentSummaries.Build(doc.ToolName, doc.Arguments),
        ResultSummary: BsonDocumentToDictionary(doc.ResultSummary));

    private static Dictionary<string, object?>? BsonDocumentToDictionary(BsonDocument? source)
    {
        if (source is null)
        {
            return null;
        }
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var element in source.Elements)
        {
            result[element.Name] = BsonValueToObject(element.Value);
        }
        return result;
    }

    internal static object? BsonValueToObject(BsonValue value) => value.BsonType switch
    {
        BsonType.Null => null,
        BsonType.String => value.AsString,
        BsonType.Int32 => value.AsInt32,
        BsonType.Int64 => value.AsInt64,
        BsonType.Double => value.AsDouble,
        BsonType.Boolean => value.AsBoolean,
        BsonType.DateTime => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc),
        BsonType.Array => value.AsBsonArray.Select(BsonValueToObject).ToArray(),
        BsonType.Document => DocumentToDictionary(value.AsBsonDocument),
        _ => value.ToString(),
    };

    private static Dictionary<string, object?> DocumentToDictionary(BsonDocument doc)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var element in doc.Elements)
        {
            result[element.Name] = BsonValueToObject(element.Value);
        }
        return result;
    }
}
