using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

public sealed record AttachIntentTerminalSkillsRequest(string IntentId, IReadOnlyList<string> SkillIds);

public sealed record AttachIntentTerminalSkillsResult(IReadOnlyList<string> AttachedSkillIds);

/// <summary>
/// Hot-attach a set of session skills into a live agent session without restarting it.
/// <list type="number">
///   <item>404 if the intent does not exist; 409 if no live tmux session — attach requires a running
///         spawn (use <c>run</c> first).</item>
///   <item>422 when any requested skill id is unknown, when it is not materialisable for the
///         current intent (e.g. <c>review</c> without an attached PR/MR), or when the live
///         vendor has no native skill hot-reload path.</item>
///   <item>Writes the canonical <c>skills/{id}/SKILL.md</c> and active vendor pointer via
///         <see cref="ISessionSkillHotAttachWriter"/>. Claude and Codex discover those files
///         natively in the live session.</item>
///   <item>Unions the requested skills into the live session's per-mode selection via
///         <see cref="IIntentTerminalLaunchStore.SaveSelectedSkillIdsAsync"/> — the single
///         «what is loaded» source — and echoes the resulting full set back. Idempotent for
///         the persisted set.</item>
/// </list>
/// </summary>
public sealed class AttachIntentTerminalSkillsHandler(
    IIntentRepository intents,
    IIntentRepositoryBindingRepository bindings,
    IIntentTerminalLaunchStore launches,
    ISessionSkillCatalog catalog,
    ITerminalVendorCatalog vendors,
    SessionSkillSelectionService selection,
    ITmuxSessionManager tmux,
    ISessionSkillHotAttachWriter writer)
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

        if (!(vendors.Find(launch.Vendor)?.SupportsNativeHotAttach ?? false))
        {
            throw new ApiException(
                TerminalErrorCodes.SessionSkillVendorUnsupported,
                "Hot-attach is only supported for vendors with native skill hot-reload.",
                new Dictionary<string, object?> { ["vendor"] = launch.Vendor });
        }

        // Materializability: reuse SessionSkillSelectionService.Validate so the review-skill ⇒
        // binding-target check stays single-sourced. Hot-attach has no PR-selection UI, so we
        // pass the implicit single-binding shape (null selectedBindingId is fine when ≤1 PR
        // attached; otherwise the validator throws ValidationFailed which we relabel below).
        var validated = ValidateMaterializable(requestedIds, await bindings.FindByIntentAsync(intent.Id, ct));

        await writer.MaterializeAsync(
            new SessionSkillPackageResolution(
                request.IntentId,
                launch.Vendor,
                validated.SelectedSkillIds,
                validated.ReviewArtifact),
            ct);

        // «What is loaded» is the live session's per-mode selection: union the just-attached
        // skills into selected_skill_ids_by_mode[liveMode] (which already holds the spawn-time
        // selection) so the badges and the next preflight see one set. Mode comes from the live
        // session's persisted axis — the same value the next preflight will look up.
        var previous = launch.SelectedSkillIdsByMode.TryGetValue(launch.Mode, out var current)
            ? current
            : Array.Empty<string>();
        var merged = previous.Concat(requestedIds).Distinct(StringComparer.Ordinal).ToArray();
        await launches.SaveSelectedSkillIdsAsync(request.IntentId, launch.Mode, merged, ct);

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
}
