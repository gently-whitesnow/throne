using Throne.Application.Events;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public abstract record RenameTagOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Renamed(Tag Tag) : RenameTagOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new TagUpdated(Tag)];
    }

    public sealed record NoChange(Tag Tag) : RenameTagOutcome;

    public sealed record NotFound : RenameTagOutcome;

    public sealed record VersionConflict(int CurrentVersion) : RenameTagOutcome;

    public sealed record NameTaken(Tag Existing) : RenameTagOutcome;
}
