using Throne.Application.Ports;

namespace Throne.Application.Intents;

/// <summary>Per-tag count keyed by the raw tag id; the API layer resolves the display name.</summary>
public sealed record IntentTagCount(string TagId, int Count);

/// <summary>
/// Aggregate counts powering the context rail. Tag breakdowns carry tag ids (not names);
/// name resolution and final ordering happen in the API layer to match the list DTO mapping.
/// </summary>
public sealed record IntentContextCounts(
    int InboxReview,
    int InboxHelp,
    int Fridge,
    int Archive,
    int Pinned,
    int Untagged,
    int ArchiveUntagged,
    IReadOnlyList<IntentTagCount> Tags,
    IReadOnlyList<IntentTagCount> ArchiveTags);

public sealed class GetIntentContextsHandler(IIntentRepository repository)
{
    public Task<IntentContextCounts> HandleAsync(CancellationToken ct) =>
        repository.GetContextCountsAsync(ct);
}
