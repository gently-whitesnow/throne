using Throne.Application.Events;

namespace Throne.Application.Ports;

public abstract record DeleteIntentOutcome : IDomainEventCarrier
{
    private DeleteIntentOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Deleted(string IntentId) : DeleteIntentOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentDeleted(IntentId)];
    }

    public sealed record NotFound : DeleteIntentOutcome;
}
