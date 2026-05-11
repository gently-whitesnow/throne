// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentStatusTools(
    SetIntentTagsHandler setTagsHandler,
    SetIntentStatusHandler setStatus)
{
    [McpServerTool(Name = "set_intent_tags", UseStructuredContent = true)]
    [Description("Replace the set of tags attached to an Intent using upsert-by-name. Pass tag names; existing tags are reused, missing tags are created.")]
    public Task<Intent> SetIntentTags(
        [Description("Intent id to mutate.")] string intent_id,
        [Description("current_version observed from the latest get_intent. Tag changes do not bump current_version but the value must still match.")] int expected_version,
        [Description("Tag names to attach (slug-style). Pass an empty array to detach all tags.")] IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default) =>
        setTagsHandler.HandleAsync(
            new SetIntentTagsCommand(intent_id, expected_version, TagIds: null, TagNames: tags),
            cancellationToken);

    [McpServerTool(Name = "mark_ready_for_review", UseStructuredContent = true)]
    [Description("Mark an Intent as ready_for_review after the agent finishes a meaningful work pass. Signals the user that the result is ready to inspect.")]
    public Task<Intent> MarkReadyForReview(
        [Description("Intent id to move into ready_for_review.")] string intent_id,
        CancellationToken cancellationToken = default) =>
        setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intent_id,
                IntentStatusNames.ReadyForReview,
                RejectReason: null,
                IntentTrainingAuthor.Agent,
                Source: "mark_ready_for_review"),
            cancellationToken);

    [McpServerTool(Name = "mark_needs_help", UseStructuredContent = true)]
    [Description("Mark an Intent as needs_help when the agent is blocked and cannot continue without operator input (missing access/info, ambiguous decision, external dependency). Use sparingly: prefer to keep working autonomously when feasible. Before calling, append a short note to Intent.text describing what is needed.")]
    public Task<Intent> MarkNeedsHelp(
        [Description("Intent id to move into needs_help.")] string intent_id,
        CancellationToken cancellationToken = default) =>
        setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intent_id,
                IntentStatusNames.NeedsHelp,
                RejectReason: null,
                IntentTrainingAuthor.Agent,
                Source: "mark_needs_help"),
            cancellationToken);
}
