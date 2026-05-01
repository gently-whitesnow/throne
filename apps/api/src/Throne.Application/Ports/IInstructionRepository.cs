using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface IInstructionRepository
{
    Task CreateAsync(Instruction instruction, TextVersion initialVersion, CancellationToken ct);

    Task<IReadOnlyList<Instruction>> GetByKindsAsync(IReadOnlyList<string> kinds, CancellationToken ct);
}
