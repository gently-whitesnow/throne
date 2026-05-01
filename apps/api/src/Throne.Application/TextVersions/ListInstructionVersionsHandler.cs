using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.TextVersions;

public sealed record ListInstructionVersionsQuery(string InstructionId);

public sealed class ListInstructionVersionsHandler(
    IInstructionRepository instructions,
    ITextVersionRepository textVersions)
{
    public async Task<IReadOnlyList<TextVersion>> HandleAsync(ListInstructionVersionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = new InstructionId(query.InstructionId);
        _ = await instructions.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"Instruction '{query.InstructionId}' not found.",
                new Dictionary<string, object?> { ["instruction_id"] = query.InstructionId });

        return await textVersions.ListByOwnerAsync(TextVersionOwnerKind.Instruction, id.Value, ct).ConfigureAwait(false);
    }
}
