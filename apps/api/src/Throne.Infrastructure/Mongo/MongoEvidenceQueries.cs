using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Reads raw evidence (intent_review, intent_qa, mcp_call_log с outcome=error)
/// for the safe window. The session-aware filter today applies only to
/// <c>mcp_call_log</c> — review/qa rows do not carry session_id, so they are
/// excluded purely by the upper safety_lag boundary.
/// </summary>
internal sealed class MongoEvidenceQueries(IMongoDatabase database) : IEvidenceQueries
{
    private readonly IMongoCollection<IntentReviewDocument> _reviews =
        database.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview);

    private readonly IMongoCollection<IntentQaDocument> _qa =
        database.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa);

    private readonly IMongoCollection<McpCallLogDocument> _mcp =
        database.GetCollection<McpCallLogDocument>(MongoCollectionNames.McpCallLog);

    public async Task<IReadOnlyList<EvidenceItemRecord>> CollectAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset sessionActivityCutoff,
        CancellationToken ct)
    {
        if (windowEnd <= windowStart)
        {
            return [];
        }

        var startUtc = windowStart.UtcDateTime;
        var endUtc = windowEnd.UtcDateTime;
        var sessionCutoffUtc = sessionActivityCutoff.UtcDateTime;

        var reviews = await _reviews
            .Find(r => r.CreatedAt >= startUtc && r.CreatedAt < endUtc)
            .ToListAsync(ct);

        var qa = await _qa
            .Find(q => q.CreatedAt >= startUtc && q.CreatedAt < endUtc)
            .ToListAsync(ct);

        var mcpErrors = await _mcp
            .Find(m => m.CreatedAt >= startUtc && m.CreatedAt < endUtc && m.Outcome == "error")
            .ToListAsync(ct);

        var activeSessions = await GetActiveSessionsAsync(sessionCutoffUtc, ct);

        var result = new List<EvidenceItemRecord>(reviews.Count + qa.Count + mcpErrors.Count);
        foreach (var r in reviews)
        {
            result.Add(new EvidenceItemRecord(
                EvidenceKindNames.Review, r.Id,
                DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                SessionId: null, HighSeverity: false));
        }
        foreach (var q in qa)
        {
            result.Add(new EvidenceItemRecord(
                EvidenceKindNames.Qa, q.Id,
                DateTime.SpecifyKind(q.CreatedAt, DateTimeKind.Utc),
                SessionId: null, HighSeverity: false));
        }
        foreach (var m in mcpErrors)
        {
            if (m.SessionId is { Length: > 0 } sid && activeSessions.Contains(sid))
            {
                continue;
            }
            result.Add(new EvidenceItemRecord(
                EvidenceKindNames.McpCall, m.Id,
                DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc),
                SessionId: m.SessionId, HighSeverity: false));
        }
        return result;
    }

    private async Task<HashSet<string>> GetActiveSessionsAsync(DateTime sessionActivityCutoffUtc, CancellationToken ct)
    {
        var ids = await _mcp
            .Find(m => m.CreatedAt >= sessionActivityCutoffUtc && m.SessionId != null)
            .Project(m => m.SessionId)
            .ToListAsync(ct);
        return new HashSet<string>(ids.Where(s => s is { Length: > 0 })!, StringComparer.Ordinal);
    }
}
