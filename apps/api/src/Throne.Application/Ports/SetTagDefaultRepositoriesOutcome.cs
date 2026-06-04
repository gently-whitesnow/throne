using Throne.Application.Events;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public abstract record SetTagDefaultRepositoriesOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Updated(Tag Tag) : SetTagDefaultRepositoriesOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new TagUpdated(Tag)];
    }

    public sealed record NoChange(Tag Tag) : SetTagDefaultRepositoriesOutcome;

    public sealed record NotFound : SetTagDefaultRepositoriesOutcome;

    public sealed record VersionConflict(int CurrentVersion) : SetTagDefaultRepositoriesOutcome;
}
