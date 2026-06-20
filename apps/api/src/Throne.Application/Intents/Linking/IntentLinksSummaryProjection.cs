using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents.Linking;

/// <summary>
/// Pure projection of <see cref="IntentLinkView"/> sequences into link-role buckets.
/// Extracted from the handler so the per-class cyclomatic budget stays low.
/// </summary>
internal static class IntentLinksSummaryProjection
{
    public static IntentLinksSummary Build(string intentId, IReadOnlyList<IntentLinkView> views)
    {
        var blockedBy = SelectPeers(views, blocking: true, IntentLinkDirection.Incoming);
        var blocks = SelectPeers(views, blocking: true, IntentLinkDirection.Outgoing);
        var linkedFrom = SelectPeers(views, blocking: false, IntentLinkDirection.Incoming);
        var linkedTo = SelectPeers(views, blocking: false, IntentLinkDirection.Outgoing);
        return new IntentLinksSummary(intentId, blockedBy, blocks, linkedFrom, linkedTo);
    }

    private static List<Intent> SelectPeers(
        IReadOnlyList<IntentLinkView> views,
        bool blocking,
        IntentLinkDirection direction)
    {
        var result = new List<Intent>();
        foreach (var view in views)
        {
            if (view.Link.Blocking == blocking && view.Direction == direction)
            {
                result.Add(view.Other);
            }
        }
        return result;
    }
}
