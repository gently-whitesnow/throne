namespace Throne.Domain.DreamRuns;

public static class DreamRunStatusNames
{
    public const string Pending = "pending";
    public const string Closed = "closed";

    public static readonly IReadOnlyList<string> All = [Pending, Closed];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public static class DreamProposalDecisionNames
{
    public const string Pending = "pending";
    public const string Applied = "applied";
    public const string Skipped = "skipped";

    public static readonly IReadOnlyList<string> All = [Pending, Applied, Skipped];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public static class DreamProposalSeverityNames
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";

    public static readonly IReadOnlyList<string> All = [High, Medium, Low];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public static class EvidenceKindNames
{
    public const string Review = "review";
    public const string Qa = "qa";
    public const string McpCall = "mcp_call";
    public const string Outcome = "outcome";
    public const string Verification = "verification";
    public const string ManualCorrection = "manual_correction";

    public static readonly IReadOnlyList<string> All =
    [
        Review,
        Qa,
        McpCall,
        Outcome,
        Verification,
        ManualCorrection,
    ];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}
