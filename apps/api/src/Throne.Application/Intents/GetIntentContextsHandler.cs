using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Application.Intents;

/// <summary>Per-tag count keyed by the raw tag id; the API layer resolves the display name.</summary>
public sealed record IntentTagCount(string TagId, int Count);

/// <summary>
/// Aggregate counts powering the context rail. Tag breakdowns carry tag ids (not names);
/// name resolution and final ordering happen in the API layer to match the list DTO mapping.
/// <c>TerminalRunning</c> is computed outside the server-side aggregation (it intersects the live
/// tmux session set with the owner's intents) — the repository leaves it 0.
/// </summary>
public sealed record IntentContextCounts(
    int InboxHelp,
    int Fridge,
    int Archive,
    int Pinned,
    int Untagged,
    int ArchiveUntagged,
    int FridgeUntagged,
    IReadOnlyList<IntentTagCount> Tags,
    IReadOnlyList<IntentTagCount> ArchiveTags,
    IReadOnlyList<IntentTagCount> FridgeTags,
    int TerminalRunning = 0);

public sealed class GetIntentContextsHandler(IIntentRepository repository, ITmuxSessionManager tmux)
{
    public async Task<IntentContextCounts> HandleAsync(CancellationToken ct)
    {
        var runningIds = await RunningTerminalIds.ResolveAsync(tmux, ct);
        return await repository.GetContextCountsAsync(runningIds, ct);
    }
}
