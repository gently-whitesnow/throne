using Throne.Application.Ports;
using Throne.Domain.Intents;

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

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.DeleteAsync(
                new IntentId(command.FromId),
                new IntentId(command.ToId),
                inner),
            ct);

        return outcome is DeleteIntentLinkOutcome.Deleted;
    }
}
