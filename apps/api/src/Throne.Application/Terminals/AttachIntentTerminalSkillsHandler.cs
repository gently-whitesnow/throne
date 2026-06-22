using System.Globalization;
using System.Text;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

public sealed record AttachIntentTerminalSkillsRequest(string IntentId, IReadOnlyList<string> SkillIds);

public sealed record AttachIntentTerminalSkillsResult(IReadOnlyList<string> AttachedSkillIds);

/// <summary>
/// Hot-attach a set of session skills into a live Claude Code session without restarting it.
/// <list type="number">
///   <item>404 if the intent does not exist; 409 if no live tmux session — attach requires a running
///         spawn (use <c>run</c> first).</item>
///   <item>422 when any requested skill id is unknown, when it is not materialisable for the
///         current intent (e.g. <c>review</c> without an attached PR/MR), or when the persisted
///         launch axis is not Claude (other vendors do not yet have a hot-attach path).</item>
///   <item>Writes <c>.claude/skills/{id}/SKILL.md</c> into the workspace via
///         <see cref="ISessionSkillHotAttachWriter"/> so a future restart picks the skill up
///         natively. Composes a single <c>&lt;system-reminder&gt;</c> message that lists every
///         attached skill with its SKILL.md text and pastes it into the pane through
///         <see cref="ITmuxSessionManager.PasteFileAsSubmittedPromptAsync"/> so the live agent
///         sees the new instructions immediately.</item>
///   <item>Persists the union with the previously hot-attached set via
///         <see cref="IIntentTerminalLaunchStore.SetAttachedSkillIdsAsync"/> and echoes the
///         resulting full set back. Idempotent for the persisted set; the reminder is re-injected
///         on every call so the operator's intent is always reflected in the live transcript.</item>
/// </list>
/// </summary>
public sealed class AttachIntentTerminalSkillsHandler(
    IIntentRepository intents,
    IIntentRepositoryBindingRepository bindings,
    IIntentTerminalLaunchStore launches,
    ISessionSkillCatalog catalog,
    SessionSkillSelectionService selection,
    ITmuxSessionManager tmux,
    ISessionSkillHotAttachWriter writer,
    TmuxPromptSubmitGate submitGate)
{
    public async Task<AttachIntentTerminalSkillsResult> HandleAsync(
        AttachIntentTerminalSkillsRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IntentId);
        ArgumentNullException.ThrowIfNull(request.SkillIds);

        var intent = await intents.GetByIdAsync(new IntentId(request.IntentId), ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{request.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = request.IntentId });

        var requestedIds = request.SkillIds.Distinct(StringComparer.Ordinal).ToArray();
        var unknown = requestedIds.Where(id => catalog.Find(id) is null).ToArray();
        if (unknown.Length > 0)
        {
            throw new ApiException(
                TerminalErrorCodes.SessionSkillUnknown,
                "Hot-attach received unknown session skill ids.",
                new Dictionary<string, object?> { ["unknown_skill_ids"] = unknown });
        }

        if (!await tmux.HasSessionAsync(request.IntentId, ct))
        {
            throw new ApiException(
                TerminalErrorCodes.SessionNotLive,
                "Hot-attach requires a live tmux session — call /terminal/run first.",
                new Dictionary<string, object?> { ["intent_id"] = request.IntentId });
        }

        var launch = await launches.GetAsync(request.IntentId, ct)
            ?? throw new ApiException(
                TerminalErrorCodes.SessionNotLive,
                "Hot-attach requires a previously spawned session for this intent.",
                new Dictionary<string, object?> { ["intent_id"] = request.IntentId });

        if (!string.Equals(launch.Vendor, TerminalAgentCatalog.VendorClaude, StringComparison.Ordinal))
        {
            throw new ApiException(
                TerminalErrorCodes.SessionSkillVendorUnsupported,
                "Hot-attach is only supported for the Claude vendor.",
                new Dictionary<string, object?> { ["vendor"] = launch.Vendor });
        }

        // Materializability: reuse SessionSkillSelectionService.Validate so the review-skill ⇒
        // binding-target check stays single-sourced. Hot-attach has no PR-selection UI, so we
        // pass the implicit single-binding shape (null selectedBindingId is fine when ≤1 PR
        // attached; otherwise the validator throws ValidationFailed which we relabel below).
        var validated = ValidateMaterializable(requestedIds, await bindings.FindByIntentAsync(intent.Id, ct));

        var materialization = await writer.MaterializeAsync(
            new SessionSkillPackageResolution(
                request.IntentId,
                launch.Vendor,
                validated.SelectedSkillIds,
                validated.ReviewArtifact),
            ct);

        var reminderText = BuildSystemReminder(materialization.Contents);
        var injectionPath = Path.Combine(
            materialization.WorkspacePath,
            $"throne-session.skill-attach.{Guid.NewGuid():N}.txt");
        Directory.CreateDirectory(materialization.WorkspacePath);
        await File.WriteAllTextAsync(injectionPath, reminderText, Encoding.UTF8, ct);
        try
        {
            await tmux.PasteFileAsSubmittedPromptAsync(request.IntentId, injectionPath, ct);
            // Same race the RunPreflightSpawn paste suffers from: the trailing send-keys Enter
            // can be absorbed by Claude's composer as newline-in-paste if it lands in the same
            // render frame as the bracketed-paste closing marker. The gate polls for the working
            // footer and re-sends Enter once before giving up.
            await submitGate.ConfirmOrThrowAsync(request.IntentId, launch.Vendor, ct);
        }
        finally
        {
            try { File.Delete(injectionPath); } catch (IOException) { /* best-effort */ }
        }

        var previous = launch.AttachedSkillIds ?? Array.Empty<string>();
        var merged = previous.Concat(requestedIds).Distinct(StringComparer.Ordinal).ToArray();
        await launches.SetAttachedSkillIdsAsync(request.IntentId, merged, ct);

        return new AttachIntentTerminalSkillsResult(merged);
    }

    private SessionSkillRunSelection ValidateMaterializable(
        IReadOnlyList<string> requestedIds,
        IReadOnlyList<Domain.Repositories.IntentRepositoryBinding> bindingList)
    {
        try
        {
            return selection.Validate(requestedIds, selectedBindingId: null, bindingList);
        }
        catch (ApiException ex) when (string.Equals(ex.Code, ErrorCodes.ValidationFailed, StringComparison.Ordinal))
        {
            // Translate the spawn-time validator's generic ValidationFailed into the attach-specific
            // codes (unknown vs not-materializable). Unknown is checked upfront, so anything that
            // slips through here is a materializability issue.
            throw new ApiException(
                TerminalErrorCodes.SessionSkillNotMaterializable,
                "Hot-attach received a skill that cannot be materialised for this intent.",
                ex.Extensions);
        }
    }

    private static string BuildSystemReminder(IReadOnlyList<HotAttachedSkillContent> contents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<system-reminder>");
        sb.AppendLine("Throne hot-attach: the following session skills have been loaded into this session.");
        sb.AppendLine();
        foreach (var c in contents)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Skill: {c.SkillId}");
            sb.AppendLine();
            sb.AppendLine(c.SkillMarkdown.TrimEnd());
            sb.AppendLine();
        }
        sb.AppendLine("</system-reminder>");
        return sb.ToString();
    }
}
