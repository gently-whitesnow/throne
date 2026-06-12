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

    [McpServerTool(Name = "set_intent_status", UseStructuredContent = true)]
    [Description(
        "Move an Intent to any workflow status. Single write-surface for status transitions — there are no per-status tools. " +
        "Agent-driven transitions you initiate on your own: 'ready_for_work' when interview is done and the formulation is clear, " +
        "'ready_for_review' when a work pass is finished and the operator can come for acceptance, " +
        "'awaiting_operator' when you stopped mid-pass and need operator input (missing access/info, ambiguous architecture, external dependency, a question to confirm) — " +
        "before calling awaiting_operator, append a short note to Intent.text describing what is needed from the operator. " +
        "Other statuses ('draft', 'interview', 'work', 'done', 'reject', 'fridge') are visible and applicable, but apply them only when the operator explicitly asks " +
        "(\"send to fridge\", \"reject this\", \"close as done\", etc.). 'interview' / 'work' are also auto-set by the server on read of the matching instruction bundle. " +
        "Reason is optional for any transition (recorded in the status-change log) and required for 'reject' (also appended to Intent.text as the rejection reason).")]
    public Task<Intent> SetIntentStatus(
        [Description("Intent id to mutate.")] string intent_id,
        [Description("Target status: draft | interview | ready_for_work | work | ready_for_review | awaiting_operator | done | reject | fridge.")] string status,
        [Description("Optional free-text reason for the transition. Recorded in intent_status_changes.reason. Required when status='reject' (also appended to Intent.text).")] string? reason = null,
        CancellationToken cancellationToken = default) =>
        setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intent_id,
                status,
                reason,
                IntentTrainingAuthor.Agent,
                Source: "set_intent_status"),
            cancellationToken);
}
