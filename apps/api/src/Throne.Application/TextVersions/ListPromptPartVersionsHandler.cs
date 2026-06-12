using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Application.TextVersions;

public sealed record ListPromptPartVersionsQuery(string PromptPartId);

public sealed class ListPromptPartVersionsHandler(
    IPromptPartRepository promptParts,
    ITextVersionRepository textVersions)
{
    public async Task<IReadOnlyList<TextVersion>> HandleAsync(ListPromptPartVersionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = new PromptPartId(query.PromptPartId);
        _ = await promptParts.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.PromptPartNotFound,
                $"Prompt part '{query.PromptPartId}' not found.",
                new Dictionary<string, object?> { ["prompt_part_id"] = query.PromptPartId });

        return await textVersions.ListByOwnerAsync(TextVersionOwnerKind.PromptPart, id.Value, ct);
    }
}
