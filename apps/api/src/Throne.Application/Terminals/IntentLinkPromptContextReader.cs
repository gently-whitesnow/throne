using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

public sealed class IntentLinkPromptContextReader(IIntentLinkRepository links)
{
    public async Task<IReadOnlyList<IntentLinkPromptContext>> BuildAsync(IntentId intentId, CancellationToken ct)
    {
        var linkList = await links.ListByIntentAsync(intentId, ct);
        return IntentLinkPromptContextBuilder.Build(linkList);
    }
}
