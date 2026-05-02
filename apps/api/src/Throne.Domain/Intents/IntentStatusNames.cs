namespace Throne.Domain.Intents;

public static class IntentStatusNames
{
    public const string Draft = "draft";
    public const string Interview = "interview";
    public const string Work = "work";
    public const string ReadyForReview = "ready_for_review";
    public const string Done = "done";
    public const string Reject = "reject";

    public static readonly IReadOnlyList<string> All =
    [
        Draft,
        Interview,
        Work,
        ReadyForReview,
        Done,
        Reject,
    ];

    public static bool IsKnown(string status) => All.Contains(status, StringComparer.Ordinal);
}
