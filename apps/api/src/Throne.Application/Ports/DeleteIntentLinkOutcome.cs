using Throne.Application.Events;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Ports;

public abstract record DeleteIntentLinkOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Deleted(IntentLink Link) : DeleteIntentLinkOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentLinkRemoved(Link)];
    }

    public sealed record NotFound : DeleteIntentLinkOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [];
    }
}
