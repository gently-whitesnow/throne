using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.TextVersions;

public sealed record ListIntentVersionsQuery(string IntentId);

public sealed class ListIntentVersionsHandler(
    IIntentRepository intents,
    ITextVersionRepository textVersions)
{
    public async Task<IReadOnlyList<TextVersion>> HandleAsync(ListIntentVersionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = new IntentId(query.IntentId);
        _ = await intents.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        return await textVersions.ListByOwnerAsync(TextVersionOwnerKind.Intent, id.Value, ct);
    }
}
