using Throne.Application.Events;

namespace Throne.Application.Ports;

public abstract record AppendTrainingOutcome : IDomainEventCarrier
{
    private AppendTrainingOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    /// <summary>
    /// Successfully appended. <paramref name="Event"/> is the concrete domain event the
    /// repository constructs from its typed input (e.g. <see cref="IntentQaAdded"/> /
    /// <see cref="IntentReviewAdded"/>); the dispatcher fans it out after commit.
    /// </summary>
    public sealed record Appended(int CurrentVersion, IDomainEvent Event) : AppendTrainingOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [Event];
    }

    public sealed record NotFound : AppendTrainingOutcome;

    public sealed record VersionConflict(int CurrentVersion) : AppendTrainingOutcome;
}
