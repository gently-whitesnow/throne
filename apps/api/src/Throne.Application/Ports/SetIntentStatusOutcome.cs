using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record SetIntentStatusOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Updated(Intent Intent) : SetIntentStatusOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentStatusChanged(Intent)];
    }

    public sealed record NotFound : SetIntentStatusOutcome;

    public sealed record Conflict(int CurrentVersion, string CurrentStatus) : SetIntentStatusOutcome;
}
