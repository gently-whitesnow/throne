using Throne.Application.Events;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Result of <see cref="IRepositoryRegistry.EnsureRepositoryAsync"/>. Mirrors
/// <see cref="EnsureTagOutcome"/>: the <see cref="Created"/> branch carries
/// <see cref="RepositoryRegistered"/> so the registry materialisation fans out exactly once,
/// on first appearance of the coordinate. The dispatching unit-of-work decorator drains the
/// events from whichever value the surrounding work returns — paths that ensure the registry
/// alongside another mutation aggregate these events onto their own outcome.
/// </summary>
public abstract record EnsureRepositoryOutcome : IDomainEventCarrier
{
    public abstract Repository Repository { get; }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Created(Repository Value) : EnsureRepositoryOutcome
    {
        public override Repository Repository => Value;
        public override IReadOnlyList<IDomainEvent> Events => [new RepositoryRegistered(Value)];
    }

    public sealed record Existed(Repository Value) : EnsureRepositoryOutcome
    {
        public override Repository Repository => Value;
    }
}
