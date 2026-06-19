namespace Throne.Application.PromptParts;

/// <summary>
/// String constants for bundle modes used by <c>get_prompt_bundle</c>. The actual list of
/// supported modes and their composition lives in the skill manifest. These constants
/// exist only as compile-time references for tests and the in-handler status mapping.
/// </summary>
public static class PromptBundleModeNames
{
    public const string Interview = "interview";
    public const string Work = "work";
    public const string Review = "review";
    public const string Dream = "dream";
}
