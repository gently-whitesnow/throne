namespace Throne.Application.DreamRuns;

/// <summary>
/// Configuration for the «dream» readiness pipeline. Bound to <c>Throne:Dream</c>
/// in appsettings; weights and thresholds are tunable without code changes.
/// See ADR-0011 for the model and starting values.
/// </summary>
public sealed class DreamOptions
{
    public const string SectionName = "Throne:Dream";

    public DreamReadinessWeights Weights { get; set; } = new();
    public DreamReadinessThresholds Thresholds { get; set; } = new();

    /// <summary>
    /// Minutes that must pass before a piece of evidence is considered «cold enough»
    /// to enter the safe window. Also the activity horizon for the session-aware filter.
    /// </summary>
    public int SafetyLagMinutes { get; set; } = 30;

    /// <summary>
    /// Hard upper bound on how far back the safe window stretches when no closed
    /// DreamRun exists yet (90 days by default).
    /// </summary>
    public int MaxWindowDays { get; set; } = 90;
}

public sealed class DreamReadinessWeights
{
    public int Review { get; set; } = 5;
    public int ReviewSeverityHigh { get; set; } = 10;
    public int VerificationFailure { get; set; } = 5;
    public int ManualCorrection { get; set; } = 8;
    public int McpCallError { get; set; } = 2;
    public int Qa { get; set; } = 1;
    public int AcceptedOutcome { get; set; } = 3;
    public int SkippedProposalWithReason { get; set; } = 4;
}

public sealed class DreamReadinessThresholds
{
    public int Ready { get; set; } = 10;
    public int Rich { get; set; } = 40;
}
