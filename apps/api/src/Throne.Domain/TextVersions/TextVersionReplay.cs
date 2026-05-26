namespace Throne.Domain.TextVersions;

/// <summary>
/// Replays a stream of <see cref="TextVersion"/> deltas to reconstruct the
/// text of an owner (Instruction / Intent) at an arbitrary historical version.
///
/// Used by read-only views that need to compare a current state against a
/// historical baseline — e.g. patch diff preview which compares
/// <c>InstructionPatch.PatchText</c> against the instruction text at
/// <c>base_instruction_version</c>.
///
/// The replay is pure: it operates on the same delta semantics that
/// <c>Instruction.ReplaceText</c>, <c>Intent.ReplaceText</c> and
/// <c>Intent.InsertTextAfterLine</c> use when writing the history, so a
/// well-formed history reconstructs verbatim.
/// </summary>
public static class TextVersionReplay
{
    /// <summary>
    /// Replay all versions up to and including <paramref name="targetVersion"/>.
    /// </summary>
    /// <param name="versions">
    /// All versions for one owner. Order is irrelevant — they are sorted by
    /// <see cref="TextVersion.Version"/> internally. Versions above
    /// <paramref name="targetVersion"/> are ignored.
    /// </param>
    /// <param name="targetVersion">
    /// Version number to reconstruct. Returns <see cref="string.Empty"/> when
    /// nothing in <paramref name="versions"/> reaches it
    /// (e.g. <c>0</c> meaning «before history»).
    /// </param>
    /// <returns>The reconstructed text, or empty string if not reconstructible.</returns>
    public static string ReplayTo(IReadOnlyList<TextVersion> versions, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var ordered = versions
            .Where(v => v.Version > 0 && v.Version <= targetVersion)
            .OrderBy(v => v.Version);
        var text = string.Empty;
        foreach (var version in ordered)
        {
            text = TextVersionDeltaApply.To(text, version);
        }
        return text;
    }
}
