using Throne.Application.Ports;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Assembles the /dream training context (ADR-0011 v3): all intents with qa/review
/// activity, minus those locked by pending DreamRuns and those consumed by closed
/// processed runs. Token counts are computed via <see cref="ContextTokenCounter"/>.
/// No time window — /dream is a manual user command, the user gates timing.
/// </summary>
public sealed class DreamWindowResolver(
    IDreamRunRepository runs,
    IIntentWindowQueries intentWindow,
    ContextTokenCounter tokenCounter)
{
    public async Task<DreamWindowAssembly> AssembleAsync(CancellationToken ct)
    {
        var raw = await intentWindow.CollectIntentsAsync(ct);
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

        var availableWindow = new IntentWindow(sortedAvailable);
        var lockedWindow = new IntentWindow(sortedLocked);
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
