using Throne.Application.Ports;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Computes the safe window (ADR-0011 v2) and assembles the <see cref="IntentWindow"/>
/// snapshot from intents whose qa/review activity falls into the window. Splits intents
/// into the «available» bucket and the «locked-by-pending-runs» bucket, and tokenises
/// both via <see cref="ContextTokenCounter"/>.
/// </summary>
public sealed class DreamWindowResolver(
    IDreamRunRepository runs,
    IIntentWindowQueries intentWindow,
    ContextTokenCounter tokenCounter,
    DreamOptions options,
    TimeProvider clock)
{
    public async Task<DreamWindowAssembly> AssembleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var safetyLag = TimeSpan.FromMinutes(options.SafetyLagMinutes);
        var maxWindow = TimeSpan.FromDays(options.MaxWindowDays);

        var windowEnd = now - safetyLag;
        var floor = now - maxWindow;

        var lastClosed = await runs.GetMostRecentClosedAsync(ct);
        var windowStart = lastClosed?.WindowEnd > floor ? lastClosed.WindowEnd : floor;
        if (windowStart >= windowEnd)
        {
            // Окно пустое (например, safety_lag > интервал между запусками); вернём
            // пустой snapshot, но с корректными границами для UI.
            var emptyWindow = new IntentWindow(windowStart, windowEnd, []);
            return new DreamWindowAssembly(emptyWindow, emptyWindow, AvailableTokens: 0, LockedTokens: 0, []);
        }

        var raw = await intentWindow.CollectIntentActivityAsync(windowStart, windowEnd, ct);
        var processed = await runs.GetProcessedIntentIdsAsync(ct);
        var locked = await runs.GetLockedIntentIdsAsync(ct);
        var processedSet = new HashSet<string>(processed, StringComparer.Ordinal);
        var lockedSet = new HashSet<string>(locked, StringComparer.Ordinal);

        var available = new List<IntentInWindow>(raw.Count);
        var lockedItems = new List<IntentInWindow>();
        foreach (var intent in raw)
        {
            if (processedSet.Contains(intent.IntentId))
            {
                continue;
            }
            if (lockedSet.Contains(intent.IntentId))
            {
                lockedItems.Add(intent);
            }
            else
            {
                available.Add(intent);
            }
        }

        // Stable order: most-recently-updated first, then by intent id.
        var sortedAvailable = available
            .OrderByDescending(i => i.UpdatedAt)
            .ThenBy(i => i.IntentId, StringComparer.Ordinal)
            .ToList();
        var sortedLocked = lockedItems
            .OrderByDescending(i => i.UpdatedAt)
            .ThenBy(i => i.IntentId, StringComparer.Ordinal)
            .ToList();

        var availableWindow = new IntentWindow(windowStart, windowEnd, sortedAvailable);
        var lockedWindow = new IntentWindow(windowStart, windowEnd, sortedLocked);
        var availableTokens = tokenCounter.Count(availableWindow);
        var lockedTokens = tokenCounter.Count(lockedWindow);
        return new DreamWindowAssembly(
            availableWindow,
            lockedWindow,
            availableTokens.TotalTokens,
            lockedTokens.TotalTokens,
            availableTokens.PerIntent);
    }
}

public sealed record DreamWindowAssembly(
    IntentWindow Available,
    IntentWindow Locked,
    int AvailableTokens,
    int LockedTokens,
    IReadOnlyList<IntentTokenBreakdown> AvailableBreakdown);
