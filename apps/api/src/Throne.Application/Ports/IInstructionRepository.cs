using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface IInstructionRepository
{
    Task CreateAsync(Instruction instruction, TextVersion initialVersion, CancellationToken ct);

    Task<IReadOnlyList<Instruction>> GetUserInstructionsByKindsAsync(
        IReadOnlyList<string> kinds,
        CancellationToken ct);

    Task<IReadOnlyList<Instruction>> ListAsync(CancellationToken ct);

    Task<Instruction?> GetByIdAsync(InstructionId id, CancellationToken ct);

    Task<ReplaceInstructionTextOutcome> ReplaceTextAsync(
        InstructionId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct);
}
