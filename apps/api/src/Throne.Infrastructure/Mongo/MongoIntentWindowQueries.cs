using MongoDB.Driver;
using Throne.Application.DreamRuns;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Reads /dream training context as full per-intent payloads. An intent enters the window
/// if any <c>intent_qa</c> or <c>intent_review</c> for it was created in
/// <c>[windowStart, windowEnd)</c>. For each such intent we then load <i>all</i> versions,
/// qa, and reviews (no window filter on the per-intent payload — once the intent is in,
/// the agent sees its complete history). MCP errors are NOT consulted (ADR-0011 v2).
/// </summary>
internal sealed class MongoIntentWindowQueries(IMongoDatabase database) : IIntentWindowQueries
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentQaDocument> _qa =
        database.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa);

    private readonly IMongoCollection<IntentReviewDocument> _reviews =
        database.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview);

    private readonly IMongoCollection<TextVersionDocument> _textVersions =
        database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);

    public async Task<IReadOnlyList<IntentInWindow>> CollectIntentActivityAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        if (windowEnd <= windowStart)
        {
            return [];
        }

        var startUtc = windowStart.UtcDateTime;
        var endUtc = windowEnd.UtcDateTime;

        var qaIds = await _qa
            .Find(q => q.CreatedAt >= startUtc && q.CreatedAt < endUtc)
            .Project(q => q.IntentId)
            .ToListAsync(ct);

        var reviewIds = await _reviews
            .Find(r => r.CreatedAt >= startUtc && r.CreatedAt < endUtc)
            .Project(r => r.IntentId)
            .ToListAsync(ct);

        var candidateIds = qaIds.Concat(reviewIds)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (candidateIds.Count == 0)
        {
            return [];
        }

        var intentDocs = await _intents
            .Find(Builders<IntentDocument>.Filter.In(d => d.Id, candidateIds))
            .ToListAsync(ct);

        var foundIds = intentDocs.Select(d => d.Id).ToList();
        if (foundIds.Count == 0)
        {
            return [];
        }

        var versionDocsTask = _textVersions
            .Find(Builders<TextVersionDocument>.Filter.And(
                Builders<TextVersionDocument>.Filter.Eq(t => t.OwnerKind, "intent"),
                Builders<TextVersionDocument>.Filter.In(t => t.OwnerId, foundIds)))
            .ToListAsync(ct);
        var qaDocsTask = _qa
            .Find(Builders<IntentQaDocument>.Filter.In(d => d.IntentId, foundIds))
            .ToListAsync(ct);
        var reviewDocsTask = _reviews
            .Find(Builders<IntentReviewDocument>.Filter.In(d => d.IntentId, foundIds))
            .ToListAsync(ct);

        await Task.WhenAll(versionDocsTask, qaDocsTask, reviewDocsTask);

        var versionsByIntent = versionDocsTask.Result
            .GroupBy(v => v.OwnerId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var qaByIntent = qaDocsTask.Result
            .GroupBy(q => q.IntentId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var reviewsByIntent = reviewDocsTask.Result
            .GroupBy(r => r.IntentId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var result = new List<IntentInWindow>(intentDocs.Count);
        foreach (var doc in intentDocs)
        {
            var versions = (versionsByIntent.GetValueOrDefault(doc.Id) ?? [])
                .Select(v => new IntentTextVersionSnapshot(
                    v.Version, v.Kind, v.Snapshot, v.OldText, v.NewText, v.InsertText))
                .ToList();
            var qa = (qaByIntent.GetValueOrDefault(doc.Id) ?? [])
                .Select(q => new IntentQaSnapshot(
                    q.Id, q.Question, q.Answer,
                    DateTime.SpecifyKind(q.CreatedAt, DateTimeKind.Utc)))
                .ToList();
            var reviews = (reviewsByIntent.GetValueOrDefault(doc.Id) ?? [])
                .Select(r => new IntentReviewSnapshot(
                    r.Id, r.Reason, r.Note,
                    DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)))
                .ToList();

            result.Add(new IntentInWindow(
                doc.Id,
                doc.Text,
                versions,
                qa,
                reviews,
                DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)));
        }
        return result;
    }
}
