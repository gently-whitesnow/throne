using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Intents;

public sealed record SetIntentTagsCommand(
    string IntentId,
    int ExpectedVersion,
    IReadOnlyList<string>? TagIds,
    IReadOnlyList<string>? TagNames);

public sealed class SetIntentTagsHandler(
    IIntentRepository intentRepository,
    IntentTagResolver tagResolver,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(SetIntentTagsCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync<SetIntentTagsHandlerOutcome>(
            async inner =>
            {
                var resolved = await tagResolver.ResolveAsync(command.TagIds, command.TagNames, now, inner);
                var setOutcome = await intentRepository.SetTagsAsync(
                    id, command.ExpectedVersion, resolved.TagIds, now, inner);
                return new SetIntentTagsHandlerOutcome(setOutcome, resolved.CreatedTags);
            },
            ct);

        return ProjectOutcome(outcome.Set, command);
    }

    private static Intent ProjectOutcome(SetIntentTagsOutcome outcome, SetIntentTagsCommand command) =>
        outcome switch
        {
            SetIntentTagsOutcome.Updated updated => updated.Intent,
            SetIntentTagsOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),
            SetIntentTagsOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.IntentVersionConflict,
                $"Intent version conflict (current_version={conflict.CurrentVersion}).",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
}

internal sealed record SetIntentTagsHandlerOutcome(
    SetIntentTagsOutcome Set,
    IReadOnlyList<Tag> CreatedTags) : Throne.Application.Events.IDomainEventCarrier
{
    public IReadOnlyList<Throne.Application.Events.IDomainEvent> Events
    {
        get
        {
            var setEvents = Set.Events;
            if (CreatedTags.Count == 0)
            {
                return setEvents;
            }

            var combined = new List<Throne.Application.Events.IDomainEvent>(setEvents.Count + CreatedTags.Count);
            foreach (var tag in CreatedTags)
            {
                combined.Add(new Throne.Application.Events.TagCreated(tag));
            }
            combined.AddRange(setEvents);
            return combined;
        }
    }
}
