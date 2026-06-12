using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

public sealed record IntentTerminalPreviewQuery(
    string IntentId,
    string Mode,
    IReadOnlyList<string>? SelectedPartIds);

/// <summary>
/// Pre-flight preview (ADR-0035): reads the intent body for the task zone and resolves the
/// embedded prompt composition for the requested mode. Unsupported modes (e.g. <c>dream</c>)
/// are rejected by <see cref="PromptCompositionResolver"/>.
/// </summary>
public sealed class IntentTerminalPreviewHandler(
    IIntentRepository intents,
    PromptCompositionResolver resolver)
{
    public async Task<PromptComposition> HandleAsync(IntentTerminalPreviewQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var intent = await intents.GetByIdAsync(new IntentId(query.IntentId), ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        return await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(query.Mode, query.SelectedPartIds, intent.State.Text),
            ct);
    }
}
