using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public sealed record GetInstructionQuery(string InstructionId);

public sealed class GetInstructionHandler(IInstructionRepository repository)
{
    public async Task<Instruction> HandleAsync(GetInstructionQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await repository.GetByIdAsync(new InstructionId(query.InstructionId), ct)
            ?? throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"Instruction '{query.InstructionId}' not found.",
                new Dictionary<string, object?> { ["instruction_id"] = query.InstructionId });
    }
}
