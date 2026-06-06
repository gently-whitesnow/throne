using Throne.Application.Git;

namespace Throne.Api.Settings;

/// <summary>
/// TTL-cached disk-usage probe for <c>Throne:Workspace:Root</c> (ADR-0024 § 1).
/// The first request after a TTL miss kicks off a background directory walk;
/// concurrent and follow-up requests see <c>status=calculating</c> with no body
/// size until the walk completes. Walk failures (e.g. permission denied on a
/// nested directory) fall back to "size unknown but ready" so the settings page
/// can still render a non-blank state. Singleton so the TTL cache survives
/// across requests; mutations guarded by a lock.
/// </summary>
public sealed class WorkspaceSizeProbe(
    IWorkspaceRootProvider workspace,
    IWorkspaceDirectorySizer sizer,
    TimeProvider clock)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private long? _cachedSizeBytes;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private Task? _backgroundWalk;

    public WorkspaceSizeSnapshot Read()
    {
        var root = workspace.ResolvedRoot;
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            var isFresh = _cachedSizeBytes is not null && (now - _cachedAt) < Ttl;
            if (isFresh)
            {
                return new WorkspaceSizeSnapshot(root, _cachedSizeBytes, IsCalculating: false);
            }

            if (_backgroundWalk is null || _backgroundWalk.IsCompleted)
            {
                _backgroundWalk = Task.Run(() => RunWalk(root));
            }

            return new WorkspaceSizeSnapshot(root, TotalSizeBytes: null, IsCalculating: true);
        }
    }

    /// <summary>
    /// Drop the cached size so the next <see cref="Read"/> re-walks the tree. Called after a
    /// workspace clean so the card recomputes (status flips back to <c>calculating</c>) instead
    /// of showing the stale pre-clean figure for up to the TTL window.
    /// </summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _cachedSizeBytes = null;
            _cachedAt = DateTimeOffset.MinValue;
        }
    }

    private void RunWalk(string root)
    {
        long total;
        try
        {
            total = sizer.Measure(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Surface partial result as "ready / size unknown" — the alternative
            // is leaving the UI in calculating-forever, which is worse UX.
            total = -1;
        }

        lock (_gate)
        {
            _cachedSizeBytes = total < 0 ? null : total;
            _cachedAt = clock.GetUtcNow();
            _backgroundWalk = null;
        }
    }
}

/// <summary>
/// Snapshot returned by <see cref="WorkspaceSizeProbe.Read"/>. <c>TotalSizeBytes</c>
/// is non-null only when <c>IsCalculating == false</c> and the most recent walk
/// reported a valid aggregate.
/// </summary>
public sealed record WorkspaceSizeSnapshot(string Root, long? TotalSizeBytes, bool IsCalculating);
