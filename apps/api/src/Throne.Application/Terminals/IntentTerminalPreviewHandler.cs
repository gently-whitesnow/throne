using Throne.Application.Errors;
using Throne.Application.Git;
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
    IWorkspaceRootProvider workspaceRoot,
    RunPreflightTagNames tagNames)
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
        // Pull the hot-attached skill ids from the persisted launch record so they survive a
        // respawn as default-on selections in the modal (see SessionSkillSelectionService).
        var launch = await launches.GetAsync(intent.Id.Value, ct);
        var skills = await skillSelection.PreviewAsync(
            intent.Id.Value, query.Mode, bindingList, launch?.AttachedSkillIds, ct);

        var workspaceMap = await ComposeWorkspaceMapAsync(intent, bindingList, ct);
        return new IntentTerminalPreview(
            composition, intent.State.CurrentVersion, skills, workspaceMap);
    }

    /// <summary>
    /// Renders the workspace map exactly as delivery does (<see cref="WorkspaceMapPrompt"/>), but with
    /// an empty body so the returned string is the map alone. Repos are listed by their (immutable)
    /// clone path without a status marker: clone paths are known the moment a binding exists and every
    /// repo is cloned by the time the agent actually reads this map at spawn, so a "still cloning" note
    /// would only be transient noise in the preview. The trailing separator/body gap is trimmed; the
    /// front renders the result read-only.
    /// </summary>
    private async Task<string> ComposeWorkspaceMapAsync(
        Intent intent, IReadOnlyList<IntentRepositoryBinding> bindings, CancellationToken ct)
    {
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intent.Id.Value);
        var repoPaths = bindings.Select(b => b.WorkspacePath).ToArray();
        var tags = await tagNames.ResolveAsync(intent.TagIds, ct);
        return WorkspaceMapPrompt.Compose(workspacePath, repoPaths, tags, userPrompt: "").TrimEnd();
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
