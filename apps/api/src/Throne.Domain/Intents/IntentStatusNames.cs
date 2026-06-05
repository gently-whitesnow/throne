namespace Throne.Domain.Intents;

public static class IntentStatusNames
{
    public const string Draft = "draft";
    public const string Interview = "interview";
    public const string ReadyForWork = "ready_for_work";
    public const string Work = "work";
    public const string ReadyForReview = "ready_for_review";
    public const string NeedsHelp = "needs_help";
    public const string Done = "done";
    public const string Reject = "reject";
    public const string Fridge = "fridge";

    public static readonly IReadOnlyList<string> All =
    [
        Draft,
        Interview,
        ReadyForWork,
        Work,
        ReadyForReview,
        NeedsHelp,
        Done,
        Reject,
        Fridge,
    ];

    public static readonly IReadOnlyList<string> Terminal = [Done, Reject, Fridge];

    public static bool IsKnown(string status) => All.Contains(status, StringComparer.Ordinal);

    public static bool IsTerminal(string status) => Terminal.Contains(status, StringComparer.Ordinal);
}
