namespace Throne.Domain.Intents.Linking;

public static class IntentLinkType
{
    public const string Relates = "relates";
    public const string Blocks = "blocks";
    public const string DerivedFrom = "derived_from";
    public const string DuplicateOf = "duplicate_of";

    public static bool IsKnown(string value) =>
        value is Relates or Blocks or DerivedFrom or DuplicateOf;

    /// <summary>
    /// Stage-1 supports relates/blocks/derived_from. duplicate_of is reserved for stage 3
    /// (merge semantics live in a separate intent).
    /// </summary>
    public static bool IsSupportedStage1(string value) =>
        value is Relates or Blocks or DerivedFrom;
}
