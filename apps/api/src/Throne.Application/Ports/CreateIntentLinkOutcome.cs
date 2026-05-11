using Throne.Application.Events;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Ports;

/// <summary>
/// Outcome of <see cref="IIntentLinkRepository.CreateAsync"/>. Success branch raises
/// <see cref="IntentLinkAdded"/>; failure branches carry no events.
/// </summary>
public abstract record CreateIntentLinkOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Created(IntentLink Link) : CreateIntentLinkOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentLinkAdded(Link)];
    }

    public sealed record IntentNotFound(string MissingIntentId) : CreateIntentLinkOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [];
    }

    public sealed record Duplicate(IntentLink Existing) : CreateIntentLinkOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [];
    }
}
