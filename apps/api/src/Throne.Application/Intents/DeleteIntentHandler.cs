using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record DeleteIntentCommand(string IntentId);

public sealed class DeleteIntentHandler(
    IIntentRepository repository,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(DeleteIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new IntentId(command.IntentId);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.DeleteAsync(id, inner),
            ct).ConfigureAwait(false);

        switch (outcome)
        {
            case DeleteIntentOutcome.Deleted:
                return;

            case DeleteIntentOutcome.NotFound:
                throw new ApiException(
                    ErrorCodes.IntentNotFound,
                    $"Intent '{command.IntentId}' not found.",
                    new Dictionary<string, object?> { ["intent_id"] = command.IntentId });

            default:
                throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}");
        }
    }
}
