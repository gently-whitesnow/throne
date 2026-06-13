using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// Paginated list handler. Owner filtering happens inside the repository; this handler
/// validates filter values and clamps <c>limit</c>.
/// </summary>
public sealed class ListPromptPartPatchesHandler(IPromptPartPatchRepository patches)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public async Task<PromptPartPatchPage> HandleAsync(ListPromptPartPatchesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.TargetScope is { } scope && !PromptPartScopeNames.IsKnown(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(query), $"Unknown target_scope filter: {scope}.");
        }
        if (query.Status is { } status && !PromptPartPatchStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(nameof(query), $"Unknown status filter: {status}.");
        }

        var limit = query.Limit ?? DefaultLimit;
        if (limit < 1)
        {
            limit = 1;
        }
        if (limit > MaxLimit)
        {
            limit = MaxLimit;
        }

        return await patches.ListAsync(
            new PromptPartPatchListFilter(query.TargetScope, query.TargetKey, query.Status),
            limit,
            query.Cursor,
            ct);
    }
}

public sealed record ListPromptPartPatchesQuery(
    string? TargetScope,
    string? TargetKey,
    string? Status,
    int? Limit,
    string? Cursor);
