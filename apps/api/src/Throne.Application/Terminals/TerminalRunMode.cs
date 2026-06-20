namespace Throne.Application.Terminals;

/// <summary>
/// Embedded run modes. They select which mandatory parts the pre-flight preview projects and the
/// spawn phase the status hooks return to (ADR-0034/0035). The embedded contour injects the
/// curated system/user context upfront rather than asking the agent to read a bundle;
/// <see cref="Free"/> has no mandatory parts (the operator curates everything).
/// </summary>
public static class TerminalRunModes
{
    public const string Work = "work";
    public const string Interview = "interview";
    public const string Review = "review";
    public const string Dream = "dream";
    public const string Free = "free";

    public static readonly IReadOnlyList<string> All = [Interview, Review, Work, Free, Dream];

    public static bool IsKnown(string value) =>
        value is Work or Interview or Review or Dream or Free;
}
