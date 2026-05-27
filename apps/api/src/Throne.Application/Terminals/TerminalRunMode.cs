namespace Throne.Application.Terminals;

/// <summary>
/// Bundle modes the spawned <c>claude</c> session is asked to read on boot. Hardcoded
/// in the prompt template <c>Прочитай бандл {mode} и {verb} интент {id}</c> — the
/// load-bearing MiniRouter trigger documented in <c>feedback_throne_bundle_prompt</c>.
/// </summary>
public static class TerminalRunModes
{
    public const string Work = "work";
    public const string Interview = "interview";
    public const string Dream = "dream";

    public static bool IsKnown(string value) =>
        value is Work or Interview or Dream;
}
