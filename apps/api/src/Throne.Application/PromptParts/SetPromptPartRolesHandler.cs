using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

public sealed record SetPromptPartRolesCommand(
    string PromptPartId,
    IReadOnlyList<PromptPartModeRole> ModeRoles);

public sealed class SetPromptPartRolesHandler(
    IPromptPartRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<PromptPart> HandleAsync(SetPromptPartRolesCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.ModeRoles);

        // Validate roles up-front so a bad request is rejected before opening the transaction.
        try
        {
            PromptPart.ValidateModeRoles(command.ModeRoles);
        }
        catch (ArgumentException ex)
        {
            throw PromptPartErrors.Validation(ex);
        }

        var now = clock.GetUtcNow();
        var part = await unitOfWork.ExecuteAsync(
            inner => repository.SetModeRolesAsync(new PromptPartId(command.PromptPartId), command.ModeRoles, now, inner),
            ct);

        return part ?? throw PromptPartErrors.NotFound(command.PromptPartId);
    }
}
