using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed record IntentTerminalPreviewQuery(
    string IntentId,
    string Mode,
    IReadOnlyList<string>? SelectedPartIds);

/// <summary>
/// Pre-flight preview result: the resolved composition plus the intent version the modal echoes
/// back as <c>expected_version</c> when it persists a task-zone edit on run.
/// </summary>
public sealed record IntentTerminalPreview(
    PromptComposition Composition,
    int IntentVersion,
    IReadOnlyList<AvailableSessionSkill> AvailableSkills,
    string WorkspaceMap);

/// <summary>
/// Pre-flight preview (ADR-0036): reads the intent body for the task zone, appends a minimal block
/// listing any current intent attachments by their workspace-relative path (the bytes are staged on
/// spawn so the embedded agent opens them with a native <c>Read</c>), and resolves the embedded prompt
/// composition for the requested mode. Unsupported modes (e.g. <c>dream</c>) are rejected by
/// <see cref="PromptCompositionResolver"/>.
///
/// Alongside the composition it returns a read-only render of the workspace map that
/// <see cref="RunPreflightPromptDelivery"/> prepends to the delivered prompt at spawn — so the modal
/// shows the real workspace root, repo paths and tags the agent will receive, not just the editable
/// body. The map is built from the same <see cref="WorkspaceMapPrompt"/> formatter but returned as a
/// separate field, never folded into <c>user_prompt</c>: the body round-trips through the run request
/// and an embedded map would be prepended a second time at delivery.
/// </summary>
public sealed class IntentTerminalPreviewHandler(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments,
    IIntentRepositoryBindingRepository bindings,
    IIntentTerminalLaunchStore launches,
    PromptCompositionResolver resolver,
    SessionSkillSelectionService skillSelection,
    IntentWorkspaceMapComposer workspaceMap)
{
    public async Task<IntentTerminalPreview> HandleAsync(IntentTerminalPreviewQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var intent = await intents.GetByIdAsync(new IntentId(query.IntentId), ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        var attachmentList = await attachments.ListByIntentAsync(intent.Id, ct);
        var bindingList = await bindings.FindByIntentAsync(intent.Id, ct);
        var userPrompt = ComposeUserPrompt(intent.State.Text, attachmentList);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(query.Mode, query.SelectedPartIds, userPrompt),
            ct);
        // Pull the per-mode «remembered» selection from the persisted launch record so the
        // modal pre-fills with the last spawn's curated set (the hot-attach handler merges
        // newly attached skills into the same map for the live session's mode).
        var launch = await launches.GetAsync(intent.Id.Value, ct);
        var remembered = launch?.SelectedSkillIdsByMode is { Count: > 0 } map
            && map.TryGetValue(query.Mode, out var ids)
                ? ids
                : null;
        var skills = await skillSelection.PreviewAsync(query.Mode, bindingList, remembered, ct);

        var workspaceMapText = await workspaceMap.ComposePreviewAsync(intent, bindingList, ct);
        return new IntentTerminalPreview(
            composition, intent.State.CurrentVersion, skills, workspaceMapText);
    }

    private static string ComposeUserPrompt(string intentText, IReadOnlyList<IntentAttachment> attachments)
    {
        var block = TerminalAttachmentsContextRenderer.Render(attachments);
        if (block is null)
        {
            return intentText;
        }
        var trimmed = intentText.TrimEnd('\r', '\n');
        return trimmed.Length == 0 ? block : $"{trimmed}\n\n{block}";
    }
}
