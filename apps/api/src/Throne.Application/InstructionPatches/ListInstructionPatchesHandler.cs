using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Paginated list handler. Owner filtering happens inside the repository; this
/// handler validates filter values and clamps <c>limit</c>.
/// </summary>
public sealed class ListInstructionPatchesHandler(IInstructionPatchRepository patches)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public async Task<InstructionPatchPage> HandleAsync(ListInstructionPatchesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.TargetKind is { } target && !InstructionKindNames.IsKnown(target))
        {
            throw new ArgumentOutOfRangeException(nameof(query), $"Unknown target_kind filter: {target}.");
        }
        if (query.Status is { } status && !InstructionPatchStatusNames.IsKnown(status))
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
            new InstructionPatchListFilter(query.TargetKind, query.Status),
            limit,
            query.Cursor,
            ct);
    }
}

public sealed record ListInstructionPatchesQuery(
    string? TargetKind,
    string? Status,
    int? Limit,
    string? Cursor);
