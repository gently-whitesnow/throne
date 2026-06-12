using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record SetIntentCleanupOnDoneCommand(string IntentId, bool Value);

public sealed class SetIntentCleanupOnDoneHandler(
    IIntentRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(SetIntentCleanupOnDoneCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();
        await unitOfWork.ExecuteAsync(
            inner => repository.SetCleanupLocalStateOnDoneAsync(id, command.Value, now, inner),
            ct);

        return await repository.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId });
    }
}
