using Throne.Domain.PromptParts;

namespace Throne.Application.Ports;

public interface IPromptPartRepository
{
    Task<CreatePromptPartOutcome> CreateAsync(PromptPart part, CancellationToken ct);

    Task<IReadOnlyList<PromptPart>> ListAsync(CancellationToken ct);

    Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct);

    Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
        int expectedVersion,
        string oldText,
        string newText,
        DateTimeOffset now,
        CancellationToken ct);

    Task<PromptPart?> SetModeRolesAsync(
        PromptPartId id,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now,
        CancellationToken ct);
}
