using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record SetIntentTitleOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Updated(Intent Intent, bool Changed) : SetIntentTitleOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events =>
            Changed ? [new IntentTitleChanged(Intent)] : [];
    }

    public sealed record NotFound : SetIntentTitleOutcome;

    public sealed record VersionConflict(int CurrentVersion) : SetIntentTitleOutcome;
}
