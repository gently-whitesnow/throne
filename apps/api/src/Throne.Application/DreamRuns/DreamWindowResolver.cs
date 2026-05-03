using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Computes the safe window (ADR-0011 §D) and assembles the <see cref="EvidenceWindow"/>
/// snapshot from raw evidence sources, filtering already-processed and locked refs.
/// </summary>
public sealed class DreamWindowResolver(
    IDreamRunRepository runs,
    IEvidenceQueries evidence,
    DreamOptions options,
    TimeProvider clock)
{
    public async Task<DreamWindowAssembly> AssembleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var safetyLag = TimeSpan.FromMinutes(options.SafetyLagMinutes);
        var maxWindow = TimeSpan.FromDays(options.MaxWindowDays);

        var windowEnd = now - safetyLag;
        var sessionCutoff = now - safetyLag;
        var floor = now - maxWindow;

        var lastClosed = await runs.GetMostRecentClosedAsync(ct);
        var windowStart = lastClosed?.WindowEnd > floor ? lastClosed.WindowEnd : floor;
        if (windowStart >= windowEnd)
        {
            // Окно пустое (например, safety_lag > интервал между запусками); вернём
            // пустой snapshot, но с корректными границами для UI.
            return new DreamWindowAssembly(
                new EvidenceWindow(windowStart, windowEnd, []),
                LockedScore: 0);
        }

        var raw = await evidence.CollectAsync(windowStart, windowEnd, sessionCutoff, ct);
        var processed = await runs.GetProcessedEvidenceAsync(ct);
        var locked = await runs.GetLockedEvidenceAsync(ct);
        var processedSet = new HashSet<(string, string)>(
            processed.Select(p => (p.Kind, p.Id)));
        var lockedSet = new HashSet<(string, string)>(
            locked.Select(l => (l.Kind, l.Id)));

        var available = new List<EvidenceItem>(raw.Count);
        var lockedItems = new List<EvidenceItem>();
        foreach (var item in raw)
        {
            var key = (item.Kind, item.Id);
            if (processedSet.Contains(key))
            {
                continue;
            }
            var mapped = new EvidenceItem(item.Kind, item.Id, item.CreatedAt, item.SessionId, item.HighSeverity);
            if (lockedSet.Contains(key))
            {
                lockedItems.Add(mapped);
            }
            else
            {
                available.Add(mapped);
            }
        }

        var window = new EvidenceWindow(windowStart, windowEnd, available);
        var lockedScore = new ReadinessCalculator(options).ScoreFor(lockedItems);
        return new DreamWindowAssembly(window, lockedScore);
    }
}

public sealed record DreamWindowAssembly(EvidenceWindow Window, int LockedScore);
