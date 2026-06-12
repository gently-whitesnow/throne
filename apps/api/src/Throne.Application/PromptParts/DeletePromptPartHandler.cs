using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

public sealed record DeletePromptPartCommand(string PromptPartId);

/// <summary>
/// Hard-deletes an operator-authored optional prompt part (ADR-0035). The part may be
/// removed only after it has been detached from every mode — the read + role check + delete
/// run inside one unit of work so the guard stays atomic with the removal.
/// </summary>
public sealed class DeletePromptPartHandler(
    IPromptPartRepository repository,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(DeletePromptPartCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.DeleteAsync(new PromptPartId(command.PromptPartId), inner),
            ct);

        switch (outcome)
        {
            case DeletePromptPartOutcome.Deleted:
                return;
            case DeletePromptPartOutcome.NotFound:
                throw PromptPartErrors.NotFound(command.PromptPartId);
            case DeletePromptPartOutcome.HasRoles:
                throw PromptPartErrors.HasRoles(command.PromptPartId);
            default:
                throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}");
        }
    }
}
