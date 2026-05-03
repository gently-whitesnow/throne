using Throne.Domain.Intents.Training;

namespace Throne.Application.Ports;

/// <summary>
/// Read-side queries over feedback evidence (intent_qa, intent_review, mcp_call_log)
/// for self-learning skills (mainly /throne investigation flow). Distinct from
/// <see cref="IIntentTrainingRepository"/>: this port is paginated and cross-intent.
/// </summary>
public interface IFeedbackQueries
{
    Task<FeedbackPage<IntentQa>> ListQaAsync(
        FeedbackListQuery query,
        CancellationToken ct);

    Task<FeedbackPage<IntentReview>> ListReviewsAsync(
        ReviewListQuery query,
        CancellationToken ct);

    Task<FeedbackPage<McpCallLogRecord>> QueryMcpCallLogAsync(
        McpCallLogQuery query,
        CancellationToken ct);
}

public sealed record FeedbackPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public sealed record FeedbackListQuery(
    string? Cursor,
    int Limit,
    DateTimeOffset Since,
    string? IntentId);

public sealed record ReviewListQuery(
    string? Cursor,
    int Limit,
    DateTimeOffset Since,
    string? IntentId,
    string? ReasonFilter);

public sealed record McpCallLogQuery(
    string? Cursor,
    int Limit,
    DateTimeOffset Since,
    string? ToolName,
    string? OutcomeFilter,
    string? IntentId,
    string? SessionId);

public sealed record McpCallLogRecord(
    string Id,
    DateTimeOffset CreatedAt,
    string ToolName,
    string Outcome,
    string? ErrorCode,
    int DurationMs,
    string? IntentId,
    string? SessionId,
    IReadOnlyDictionary<string, object?>? ArgumentSummary,
    IReadOnlyDictionary<string, object?>? ResultSummary);
