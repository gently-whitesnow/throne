using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Application.PromptPartPatches;

public sealed class GetPromptPartPatchHandler(
    IPromptPartPatchRepository patches,
    UserPromptPartLookup partLookup,
    ITextVersionRepository textVersions)
{
    public async Task<PromptPartPatchView> HandleAsync(string patchId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchId);

        var patch = await patches.GetAsync(patchId, ct)
            ?? throw PromptPartPatchExceptions.NotFound(patchId);

        var part = await partLookup.FindAsync(patch.Identity.TargetScope, patch.Identity.TargetKey, ct);

        var baseText = await ReadBasePartTextAsync(patch, part, ct);
        return PromptPartPatchView.From(patch, part, baseText);
    }

    /// <summary>
    /// Replay the part's text-version history up to <c>patch.base_version</c> so the UI can show
    /// a clean diff. Returns empty string when there is no history yet (<c>base_version=0</c>) or
    /// when the underlying part was deleted.
    /// </summary>
    private async Task<string> ReadBasePartTextAsync(
        PromptPartPatch patch, PromptPart? part, CancellationToken ct)
    {
        if (part is null || patch.Identity.BaseVersion <= 0)
        {
            return string.Empty;
        }
        if (patch.Identity.BaseVersion == part.CurrentVersion)
        {
            return part.Text;
        }

        var versions = await textVersions.ListByOwnerAsync(
            TextVersionOwnerKind.PromptPart, part.Id.Value, ct);
        return TextVersionReplay.ReplayTo(versions, patch.Identity.BaseVersion);
    }
}

public sealed record PromptPartPatchView(
    PromptPartPatch Patch,
    string CurrentPartText,
    int CurrentPartVersion,
    bool BaseVersionMatchesCurrent,
    string BasePartText)
{
    public static PromptPartPatchView From(PromptPartPatch patch, PromptPart? part, string basePartText) => new(
        patch,
        part?.Text ?? string.Empty,
        part?.CurrentVersion ?? 0,
        part is not null && part.CurrentVersion == patch.Identity.BaseVersion,
        basePartText);
}
