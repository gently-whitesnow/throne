using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Application.Intents;

public sealed record CreateIntentCommand(
    string Text,
    IReadOnlyList<string>? TagNames,
    TextVersionAuthor Author);

public sealed class CreateIntentHandler(
    IIntentRepository repository,
    IntentTagResolver tagResolver,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(CreateIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.GetUtcNow();
        var id = IntentId.New();
        var ownerUserId = currentUser.UserId;

        var outcome = await unitOfWork.ExecuteAsync(
            async inner =>
            {
                var resolved = await tagResolver.EnsureByNamesAsync(command.TagNames, now, inner);
                var sortKey = await NextTopSortKeyAsync(inner);
                var intent = IntentFactory.Create(id, ownerUserId, command.Text, resolved.TagIds, now, sortKey: sortKey);
                var initialVersion = TextVersion.CreateSnapshot(
                    id: Guid.NewGuid().ToString("N"),
                    ownerKind: TextVersionOwnerKind.Intent,
                    ownerId: id.Value,
                    snapshot: intent.State.Text,
                    changedAt: now,
                    changedBy: command.Author);
                var initialStatusChange = IntentStatusChange.Create(
                    id: Guid.NewGuid().ToString("N"),
                    intentId: id,
                    intentVersionAtWrite: intent.State.CurrentVersion,
                    fromStatus: intent.State.Status,
                    toStatus: intent.State.Status,
                    source: "create_intent",
                    createdAt: now,
                    createdBy: TextVersionAuthorMapping.ToTrainingAuthor(command.Author));

                return await repository.CreateAsync(
                    intent, initialVersion, initialStatusChange, resolved.CreatedTags, inner);
            },
            ct);

        return outcome.Intent;
    }

    private async Task<string> NextTopSortKeyAsync(CancellationToken ct)
    {
        var currentMin = await repository.GetMinSortKeyAsync(ct);
        return FractionalIndex.Between(
            before: null,
            after: string.IsNullOrEmpty(currentMin) ? null : currentMin);
    }
}
