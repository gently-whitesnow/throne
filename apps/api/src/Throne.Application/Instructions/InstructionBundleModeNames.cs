namespace Throne.Application.Instructions;

/// <summary>
/// String constants for bundle modes used by <c>get_instruction_bundle</c>.
/// The actual list of supported modes and their kind composition lives in the
/// skill manifest (see <see cref="Manifest.ISkillManifestProvider"/>).
/// These constants exist only as compile-time references for tests and the
/// in-handler status mapping; they intentionally do NOT enumerate the truth.
/// </summary>
public static class InstructionBundleModeNames
{
    public const string Interview = "interview";
    public const string Work = "work";
    public const string NewProject = "new_project";
    public const string Dream = "dream";
    public const string Fix = "fix";
}
