using Throne.Application.Ports;

namespace Throne.Application.Terminals;

public sealed record IntentLinkPromptContext(string Label, string PeerIntentId, string Status, string? Rationale);

internal static class IntentLinkPromptContextBuilder
{
    public static IReadOnlyList<IntentLinkPromptContext> Build(IReadOnlyList<IntentLinkView> links)
    {
        var result = new List<IntentLinkPromptContext>();
        foreach (var view in links)
        {
            var rationale = string.IsNullOrWhiteSpace(view.Link.Rationale)
                ? null
                : view.Link.Rationale.Trim();
            if (!view.Link.Blocking && rationale is null)
            {
                continue;
            }

            result.Add(new IntentLinkPromptContext(
                LinkLabel(view.Link.Blocking, view.Direction == IntentLinkDirection.Outgoing),
                view.Other.Id.Value,
                view.Other.State.Status,
                rationale));
        }

        return result;
    }

    private static string LinkLabel(bool blocking, bool isOutgoing)
    {
        if (blocking)
        {
            return isOutgoing ? "блокирует" : "заблокирован";
        }

        return isOutgoing ? "ведёт к" : "вытекает из";
    }
}
