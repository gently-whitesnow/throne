using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public sealed record ListInstructionsQuery();

public sealed class ListInstructionsHandler(IInstructionRepository repository)
{
    public async Task<IReadOnlyList<Instruction>> HandleAsync(ListInstructionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await repository.ListAsync(ct);
    }
}
