namespace Throne.Application.DreamRuns;

/// <summary>
/// Configuration for the «dream» readiness pipeline. Bound to <c>Throne:Dream</c> in
/// appsettings. ADR-0011 v2 keeps only the safe-window controls — weights and thresholds
/// are gone with the move to a token-counter readiness model.
/// </summary>
public sealed class DreamOptions
{
    public const string SectionName = "Throne:Dream";

    /// <summary>
    /// Minutes that must pass before a piece of evidence is considered «cold enough»
    /// to enter the safe window.
    /// </summary>
    public int SafetyLagMinutes { get; set; } = 30;

    /// <summary>
    /// Hard upper bound on how far back the safe window stretches when no closed
    /// DreamRun exists yet (90 days by default).
    /// </summary>
    public int MaxWindowDays { get; set; } = 90;
}
