using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Instructions;

public sealed record CreateInstructionCommand(string Kind, string Text);

/// <summary>
/// Создаёт user-инструкцию заданного <see cref="InstructionKindNames"/> для текущего пользователя.
/// Если запись уже существует — 409 (<see cref="ErrorCodes.InstructionAlreadyExists"/>);
/// для правки текста используется replace-text.
/// </summary>
public sealed class CreateInstructionHandler(
    IInstructionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<Instruction> HandleAsync(CreateInstructionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Text);
        if (!InstructionKindNames.IsKnown(command.Kind))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown instruction kind: {command.Kind}.",
                new Dictionary<string, object?> { ["field"] = "kind" });
        }

        var ownerUserId = currentUser.UserId;
        var existing = await repository.GetUserInstructionsByKindsAsync(ownerUserId, [command.Kind], ct);
        if (existing.Count > 0)
        {
            throw new ApiException(
                ErrorCodes.InstructionAlreadyExists,
                $"User instruction with kind '{command.Kind}' already exists; use replace-text to edit it.",
                new Dictionary<string, object?>
                {
                    ["kind"] = command.Kind,
                    ["instruction_id"] = existing[0].Id.Value,
                });
        }

        var now = clock.GetUtcNow();
        var instruction = Instruction.Create(
            id: InstructionId.New(),
            scope: InstructionScopeNames.User,
            userId: ownerUserId,
            kind: command.Kind,
            text: command.Text,
            now: now);
        var initialVersion = TextVersion.CreateSnapshot(
            id: Guid.NewGuid().ToString("N"),
            ownerKind: TextVersionOwnerKind.Instruction,
            ownerId: instruction.Id.Value,
            snapshot: instruction.Text,
            changedAt: now,
            changedBy: TextVersionAuthor.User);

        await unitOfWork.ExecuteAsync(
            inner => repository.CreateAsync(instruction, initialVersion, inner),
            ct);

        return instruction;
    }
}
