using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Snapshot of raw evidence available in a safe time window (see ADR-0011).
/// Used by <see cref="ReadinessCalculator"/> and as raw input for `run_dream` (Intent 4).
/// </summary>
public sealed record EvidenceWindow(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<EvidenceItem> Items);

/// <summary>
/// One piece of raw evidence within the window. SessionId and HighSeverity are
/// honored by the readiness calculator and the session-aware filter.
/// </summary>
public sealed record EvidenceItem(
    string Kind,
    string Id,
    DateTimeOffset CreatedAt,
    string? SessionId,
    bool HighSeverity)
{
    public EvidenceRef ToRef() => new(Kind, Id, CreatedAt);
}
