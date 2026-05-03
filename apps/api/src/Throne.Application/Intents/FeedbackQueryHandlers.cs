using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Intents;

internal static class FeedbackQueryDefaults
{
    public const int DefaultListLimit = 50;
    public const int MaxListLimit = 200;
    public const int DefaultMcpLimit = 100;
    public const int MaxMcpLimit = 500;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(90);

    public static DateTimeOffset ResolveSince(DateTimeOffset? since, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var earliestAllowed = now - MaxWindow;
        var resolved = since ?? now - DefaultWindow;
        return resolved < earliestAllowed ? earliestAllowed : resolved;
    }

    public static int ResolveLimit(int? requested, int @default, int max)
    {
        if (requested is null)
        {
            return @default;
        }
        if (requested.Value <= 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "limit must be a positive integer.",
                new Dictionary<string, object?> { ["limit"] = requested.Value });
        }
        return Math.Min(requested.Value, max);
    }
}

public sealed record ListIntentReviewsForFeedbackQuery(
    string? Cursor,
    int? Limit,
    DateTimeOffset? Since,
    string? ReasonFilter,
    string? IntentId);

public sealed class ListIntentReviewsForFeedbackHandler(
    IFeedbackQueries queries,
    TimeProvider clock)
{
    public Task<FeedbackPage<IntentReview>> HandleAsync(
        ListIntentReviewsForFeedbackQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        return queries.ListReviewsAsync(
            new ReviewListQuery(
                Cursor: string.IsNullOrWhiteSpace(query.Cursor) ? null : query.Cursor,
                Limit: FeedbackQueryDefaults.ResolveLimit(
                    query.Limit, FeedbackQueryDefaults.DefaultListLimit, FeedbackQueryDefaults.MaxListLimit),
                Since: FeedbackQueryDefaults.ResolveSince(query.Since, clock),
                IntentId: string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId,
                ReasonFilter: string.IsNullOrWhiteSpace(query.ReasonFilter) ? null : query.ReasonFilter),
            ct);
    }
}

public sealed record ListIntentQaForFeedbackQuery(
    string? Cursor,
    int? Limit,
    DateTimeOffset? Since,
    string? IntentId);

public sealed class ListIntentQaForFeedbackHandler(
    IFeedbackQueries queries,
    TimeProvider clock)
{
    public Task<FeedbackPage<IntentQa>> HandleAsync(
        ListIntentQaForFeedbackQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        return queries.ListQaAsync(
            new FeedbackListQuery(
                Cursor: string.IsNullOrWhiteSpace(query.Cursor) ? null : query.Cursor,
                Limit: FeedbackQueryDefaults.ResolveLimit(
                    query.Limit, FeedbackQueryDefaults.DefaultListLimit, FeedbackQueryDefaults.MaxListLimit),
                Since: FeedbackQueryDefaults.ResolveSince(query.Since, clock),
                IntentId: string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId),
            ct);
    }
}

public sealed record QueryMcpCallLogQuery(
    string? Cursor,
    int? Limit,
    DateTimeOffset? Since,
    string? ToolName,
    string? OutcomeFilter,
    string? IntentId,
    string? SessionId);

public sealed class QueryMcpCallLogHandler(
    IFeedbackQueries queries,
    TimeProvider clock)
{
    private static readonly string[] AllowedOutcomes = ["success", "error"];

    public Task<FeedbackPage<McpCallLogRecord>> HandleAsync(
        QueryMcpCallLogQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var outcome = string.IsNullOrWhiteSpace(query.OutcomeFilter) ? null : query.OutcomeFilter;
        if (outcome is not null && Array.IndexOf(AllowedOutcomes, outcome) < 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "outcome_filter must be 'success' or 'error'.",
                new Dictionary<string, object?>
                {
                    ["outcome_filter"] = outcome,
                    ["allowed_values"] = AllowedOutcomes,
                });
        }

        return queries.QueryMcpCallLogAsync(
            new McpCallLogQuery(
                Cursor: string.IsNullOrWhiteSpace(query.Cursor) ? null : query.Cursor,
                Limit: FeedbackQueryDefaults.ResolveLimit(
                    query.Limit, FeedbackQueryDefaults.DefaultMcpLimit, FeedbackQueryDefaults.MaxMcpLimit),
                Since: FeedbackQueryDefaults.ResolveSince(query.Since, clock),
                ToolName: string.IsNullOrWhiteSpace(query.ToolName) ? null : query.ToolName,
                OutcomeFilter: outcome,
                IntentId: string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId,
                SessionId: string.IsNullOrWhiteSpace(query.SessionId) ? null : query.SessionId),
            ct);
    }
}
