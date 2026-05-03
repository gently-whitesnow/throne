// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents.Training;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class FeedbackTools(
    ListIntentReviewsForFeedbackHandler listReviews,
    ListIntentQaForFeedbackHandler listQa,
    QueryMcpCallLogHandler queryCallLog,
    GetUserInstructionHandler getUserInstruction)
{
    [McpServerTool(Name = "list_intent_reviews", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read paginated intent_review training records (post-work corrections). Used by /throne for self-learning evidence; not exposed to /tdream or /twork. Returns at most `limit` items plus an opaque next_cursor when more are available.")]
    public async Task<FeedbackPageDto<IntentReviewDto>> ListIntentReviews(
        [Description("Opaque cursor returned by a previous call. Pass null on the first page.")] string? cursor = null,
        [Description("Maximum items in the page. Defaults to 50, hard cap 200.")] int? limit = null,
        [Description("Lower bound (inclusive) of created_at. Defaults to now-30d; values older than now-90d are clamped.")] DateTimeOffset? since = null,
        [Description("Case-insensitive substring match against the review reason.")] string? reason_filter = null,
        [Description("Optional intent id to scope the listing to a single intent.")] string? intent_id = null,
        CancellationToken cancellationToken = default)
    {
        var page = await listReviews.HandleAsync(
            new ListIntentReviewsForFeedbackQuery(cursor, limit, since, reason_filter, intent_id),
            cancellationToken);

        var items = page.Items.Select(r => new IntentReviewDto(
            r.Id,
            r.IntentId.Value,
            r.IntentVersionAtWrite,
            r.Note,
            r.Reason,
            r.CreatedAt,
            AuthorWire(r.CreatedBy))).ToArray();

        return new FeedbackPageDto<IntentReviewDto>(items, page.NextCursor);
    }

    [McpServerTool(Name = "list_intent_qa", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read paginated intent_qa training records (interview questions/answers). Used by /throne for self-learning evidence. Returns at most `limit` items plus an opaque next_cursor when more are available.")]
    public async Task<FeedbackPageDto<IntentQaDto>> ListIntentQa(
        [Description("Opaque cursor returned by a previous call. Pass null on the first page.")] string? cursor = null,
        [Description("Maximum items in the page. Defaults to 50, hard cap 200.")] int? limit = null,
        [Description("Lower bound (inclusive) of created_at. Defaults to now-30d; values older than now-90d are clamped.")] DateTimeOffset? since = null,
        [Description("Optional intent id to scope the listing to a single intent.")] string? intent_id = null,
        CancellationToken cancellationToken = default)
    {
        var page = await listQa.HandleAsync(
            new ListIntentQaForFeedbackQuery(cursor, limit, since, intent_id),
            cancellationToken);

        var items = page.Items.Select(q => new IntentQaDto(
            q.Id,
            q.IntentId.Value,
            q.IntentVersionAtWrite,
            q.Question,
            q.Answer,
            q.CreatedAt,
            AuthorWire(q.CreatedBy))).ToArray();

        return new FeedbackPageDto<IntentQaDto>(items, page.NextCursor);
    }

    [McpServerTool(Name = "query_mcp_call_log", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read paginated mcp_call_log records with filters. Used by /throne to investigate recent agent behavior. Argument payloads are projected to a safe whitelist (argument_summary); raw arguments are never returned.")]
    public async Task<FeedbackPageDto<McpCallLogDto>> QueryMcpCallLog(
        [Description("Opaque cursor returned by a previous call. Pass null on the first page.")] string? cursor = null,
        [Description("Maximum items in the page. Defaults to 100, hard cap 500.")] int? limit = null,
        [Description("Lower bound (inclusive) of created_at. Defaults to now-30d; values older than now-90d are clamped.")] DateTimeOffset? since = null,
        [Description("Filter by exact tool name (e.g. 'replace_intent_text').")] string? tool_name = null,
        [Description("Filter by call outcome: 'success' or 'error'.")] string? outcome_filter = null,
        [Description("Filter by intent id captured at call time.")] string? intent_id = null,
        [Description("Filter by MCP session id.")] string? session_id = null,
        CancellationToken cancellationToken = default)
    {
        var page = await queryCallLog.HandleAsync(
            new QueryMcpCallLogQuery(cursor, limit, since, tool_name, outcome_filter, intent_id, session_id),
            cancellationToken);

        var items = page.Items.Select(r => new McpCallLogDto(
            r.Id,
            r.CreatedAt,
            r.ToolName,
            r.Outcome,
            r.ErrorCode,
            r.DurationMs,
            r.IntentId,
            r.SessionId,
            r.ArgumentSummary,
            r.ResultSummary)).ToArray();

        return new FeedbackPageDto<McpCallLogDto>(items, page.NextCursor);
    }

    private static string AuthorWire(IntentTrainingAuthor author) => author switch
    {
        IntentTrainingAuthor.Agent => "agent",
        IntentTrainingAuthor.User => "user",
        IntentTrainingAuthor.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(author), author, null),
    };

    [McpServerTool(Name = "get_user_instruction", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read the current user-scoped instruction for a given kind. Used only by /throne for instruction drafting; /tdream relies on aggregated DreamContextPack instead.")]
    public async Task<UserInstructionDto> GetUserInstruction(
        [Description("Instruction kind to fetch (e.g. 'work', 'common').")] string kind,
        CancellationToken cancellationToken = default)
    {
        var result = await getUserInstruction.HandleAsync(new GetUserInstructionQuery(kind), cancellationToken);
        return new UserInstructionDto(result.InstructionId, result.Kind, result.CurrentVersion, result.Text);
    }
}

public sealed record FeedbackPageDto<T>(
    [property: Description("Page items in stable (created_at, id) order.")] IReadOnlyList<T> Items,
    [property: Description("Opaque cursor for the next page, or null when fully drained.")] string? NextCursor);

public sealed record IntentReviewDto(
    [property: Description("Review record identifier.")] string Id,
    [property: Description("Owning intent identifier.")] string IntentId,
    [property: Description("Intent.current_version observed when the review was appended.")] int IntentVersionAtWrite,
    [property: Description("The user's concrete correction or complaint.")] string Note,
    [property: Description("Why the correction matters, or what the agent misunderstood.")] string Reason,
    [property: Description("Creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Author of the review: 'agent', 'user', or 'system'.")] string CreatedBy);

public sealed record IntentQaDto(
    [property: Description("QA record identifier.")] string Id,
    [property: Description("Owning intent identifier.")] string IntentId,
    [property: Description("Intent.current_version observed when the QA was appended.")] int IntentVersionAtWrite,
    [property: Description("The exact useful question the agent asked the user.")] string Question,
    [property: Description("The user's answer captured during interview.")] string Answer,
    [property: Description("Creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Author of the QA: 'agent', 'user', or 'system'.")] string CreatedBy);

public sealed record McpCallLogDto(
    [property: Description("Call log entry identifier.")] string Id,
    [property: Description("Call timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Tool name as registered with the MCP server.")] string ToolName,
    [property: Description("Call outcome: 'success' or 'error'.")] string Outcome,
    [property: Description("Application error code on failure (e.g. 'intent.not_found'); null on success.")] string? ErrorCode,
    [property: Description("Wall-clock duration in milliseconds.")] int DurationMs,
    [property: Description("Intent id captured from arguments, if any.")] string? IntentId,
    [property: Description("MCP session id captured at call time.")] string? SessionId,
    [property: Description("Whitelisted, safe projection of the call arguments. Null for tools that are not in the whitelist.")] IReadOnlyDictionary<string, object?>? ArgumentSummary,
    [property: Description("Summarized structured result captured by the audit middleware, if any.")] IReadOnlyDictionary<string, object?>? ResultSummary);

public sealed record UserInstructionDto(
    [property: Description("Instruction identifier.")] string InstructionId,
    [property: Description("Instruction kind (e.g. 'work', 'common').")] string Kind,
    [property: Description("Current text version of the instruction.")] int CurrentVersion,
    [property: Description("Full instruction text, exactly as authored.")] string Text);
