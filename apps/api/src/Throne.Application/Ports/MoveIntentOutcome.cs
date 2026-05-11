using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record MoveIntentOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Moved(Intent Intent, bool Changed) : MoveIntentOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events =>
            Changed ? [new IntentReordered(Intent)] : [];
    }

    public sealed record NotFound : MoveIntentOutcome;

    public sealed record PivotNotFound(string PivotId) : MoveIntentOutcome;
}
