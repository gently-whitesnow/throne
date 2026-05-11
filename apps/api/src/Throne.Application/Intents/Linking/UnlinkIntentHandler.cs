using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Intents.Linking;

public sealed class UnlinkIntentHandler(
    IIntentLinkRepository repository,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Idempotent: a missing edge returns success-without-event so the agent can retry the
    /// rollback without surfacing a not-found.
    /// </summary>
    public async Task<bool> HandleAsync(UnlinkIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FromId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ToId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Type);

        if (!IntentLinkType.IsKnown(command.Type))
        {
            return false;
        }

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.DeleteAsync(
                new IntentId(command.FromId),
                new IntentId(command.ToId),
                command.Type,
                inner),
            ct);

        return outcome is DeleteIntentLinkOutcome.Deleted;
    }
}
