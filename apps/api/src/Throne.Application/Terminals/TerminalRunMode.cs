namespace Throne.Application.Terminals;

/// <summary>
/// Bundle modes the spawned <c>claude</c> session is asked to read on boot. For the bundle
/// modes the prompt is hardcoded in the template <c>Прочитай бандл {mode} и {verb} интент {id}</c>
/// — the load-bearing MiniRouter trigger documented in <c>feedback_throne_bundle_prompt</c>.
/// <see cref="Free"/> reads no bundle: claude boots bare and the editable text is pre-typed
/// into its prompt for the operator to finish.
/// </summary>
public static class TerminalRunModes
{
    public const string Work = "work";
    public const string Interview = "interview";
    public const string Dream = "dream";
    public const string Free = "free";

    public static bool IsKnown(string value) =>
        value is Work or Interview or Dream or Free;
}
