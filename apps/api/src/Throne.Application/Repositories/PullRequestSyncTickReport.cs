using Throne.Application.Git;

namespace Throne.Application.Repositories;

/// <summary>
/// Per-tick observability surface for the polling loop. Exposes a single
/// <see cref="Snapshot"/> projection so callers (host logging, tests) get a
/// strongly-typed view of the counters.
/// </summary>
public sealed class PullRequestSyncTickReport
{
    private readonly PullRequestSyncCounters _counters = new();
    private readonly List<PullRequestSyncFailure> _failures = [];

    internal PullRequestSyncCounters Counters => _counters;

    public IReadOnlyList<PullRequestSyncFailure> Failures => _failures;

    public PullRequestSyncTickSnapshot Snapshot =>
        new(
            Polled: _counters.Polled,
            NotModified: _counters.NotModified,
            NewComments: _counters.NewComments,
            Skipped: _counters.Skipped,
            Failed: _counters.Failed,
            MarkedBroken: _counters.MarkedBroken,
            LifecycleClosed: _counters.LifecycleClosed);

    internal void AddFailure(PullRequestSyncFailure failure) => _failures.Add(failure);
}

/// <summary>Immutable snapshot of a tick's counters. Returned by
/// <see cref="PullRequestSyncTickReport.Snapshot"/>; consumed by the host's
/// structured log and by tests.</summary>
public sealed record PullRequestSyncTickSnapshot(
    int Polled,
    int NotModified,
    int NewComments,
    int Skipped,
    int Failed,
    int MarkedBroken,
    int LifecycleClosed);

internal sealed class PullRequestSyncCounters
{
    public int Polled;
    public int NotModified;
    public int NewComments;
    public int Skipped;
    public int Failed;
    public int MarkedBroken;
    public int LifecycleClosed;
}

public sealed record PullRequestSyncFailure(string BindingId, GitProviderErrorKind Kind, string Message);
