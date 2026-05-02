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
    ITagRepository tagRepository,
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
                var resolved = await ResolveTagIdsAsync(command, now, inner);
                var setOutcome = await intentRepository
                    .SetTagsAsync(id, command.ExpectedVersion, resolved.TagIds, now, inner)
                    ;
                return new SetIntentTagsHandlerOutcome(setOutcome, resolved.CreatedTags);
            },
            ct);

        return outcome.Set switch
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
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.Set.GetType().Name}"),
        };
    }

    private async Task<(IReadOnlyList<TagId> TagIds, IReadOnlyList<Tag> CreatedTags)> ResolveTagIdsAsync(
        SetIntentTagsCommand command,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tagIds = new List<TagId>();
        var created = new List<Tag>();

        if (command.TagIds is { Count: > 0 })
        {
            foreach (var raw in command.TagIds)
            {
                if (string.IsNullOrWhiteSpace(raw) || !seen.Add(raw))
                {
                    continue;
                }

                var tagId = new TagId(raw);
                _ = await tagRepository.GetByIdAsync(tagId, ct)
                    ?? throw new ApiException(
                        ErrorCodes.TagNotFound,
                        $"Tag '{raw}' not found.",
                        new Dictionary<string, object?> { ["tag_id"] = raw });
                tagIds.Add(tagId);
            }
        }

        if (command.TagNames is { Count: > 0 })
        {
            foreach (var raw in command.TagNames)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string normalized;
                try
                {
                    normalized = TagNames.Normalize(raw);
                }
                catch (ArgumentException ex)
                {
                    throw new ApiException(
                        ErrorCodes.TagNameInvalid,
                        ex.Message,
                        new Dictionary<string, object?> { ["name"] = raw });
                }

                var ensure = await tagRepository.EnsureByNameAsync(normalized, now, ct);
                if (!seen.Add(ensure.Tag.Id.Value))
                {
                    continue;
                }

                tagIds.Add(ensure.Tag.Id);
                if (ensure is EnsureTagOutcome.Created createdOutcome)
                {
                    created.Add(createdOutcome.Value);
                }
            }
        }

        return (tagIds, created);
    }
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
