using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Intents.Linking;

/// <summary>
/// Pure projection of <see cref="IntentLinkView"/> sequences into the four link-role
/// buckets surfaced on the board (`blocked_by`, `derived_from`, `source_of`, `relates`).
/// Extracted from the handler so the per-class cyclomatic budget stays low.
/// </summary>
internal static class IntentLinksSummaryProjection
{
    public static IntentLinksSummary Build(string intentId, IReadOnlyList<IntentLinkView> views)
    {
        var blockedBy = SelectPeers(views, IntentLinkType.Blocks, IntentLinkDirection.Incoming);
        var derivedFrom = SelectPeers(views, IntentLinkType.DerivedFrom, IntentLinkDirection.Outgoing);
        var sourceOf = SelectPeers(views, IntentLinkType.DerivedFrom, IntentLinkDirection.Incoming);
        var relates = DistinctRelatesPeers(views);
        return new IntentLinksSummary(intentId, blockedBy, derivedFrom, sourceOf, relates);
    }

    private static List<Intent> SelectPeers(
        IReadOnlyList<IntentLinkView> views,
        string type,
        IntentLinkDirection direction)
    {
        var result = new List<Intent>();
        foreach (var view in views)
        {
            if (view.Link.Type == type && view.Direction == direction)
            {
                result.Add(view.Other);
            }
        }
        return result;
    }

    private static List<Intent> DistinctRelatesPeers(IReadOnlyList<IntentLinkView> views)
    {
        var seen = new Dictionary<string, Intent>(StringComparer.Ordinal);
        foreach (var view in views)
        {
            if (view.Link.Type == IntentLinkType.Relates)
            {
                seen.TryAdd(view.Other.Id.Value, view.Other);
            }
        }
        return [.. seen.Values];
    }
}
