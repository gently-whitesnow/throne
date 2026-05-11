using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record SetIntentTagsOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Updated(Intent Intent, bool Changed) : SetIntentTagsOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events =>
            Changed ? [new IntentTagsChanged(Intent)] : [];
    }

    public sealed record NotFound : SetIntentTagsOutcome;

    public sealed record VersionConflict(int CurrentVersion) : SetIntentTagsOutcome;
}
