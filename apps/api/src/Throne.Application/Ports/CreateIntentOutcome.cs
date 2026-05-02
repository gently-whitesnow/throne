using Throne.Application.Events;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

/// <summary>
/// Outcome wrapper for a successful intent creation. Always carries
/// <see cref="IntentCreated"/>; failure paths throw via the repository (creation has
/// no domain-level branching today). Also carries one <see cref="TagCreated"/> event
/// per tag that was upserted by name as part of this create call.
/// </summary>
public sealed record CreateIntentOutcome(Intent Intent, IReadOnlyList<Tag> CreatedTags) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events
    {
        get
        {
            var events = new List<IDomainEvent>(CreatedTags.Count + 1);
            foreach (var tag in CreatedTags)
            {
                events.Add(new TagCreated(tag));
            }
            events.Add(new IntentCreated(Intent));
            return events;
        }
    }
}
