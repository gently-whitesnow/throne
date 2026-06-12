using Throne.Application.Events;
using Throne.Domain.PromptParts;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence result of <see cref="IPromptPartPatchRepository.CreateAsync"/>.
/// Carries a <c>PromptPartPatchProposed</c> domain event for fresh inserts.
/// When <c>IsExisting</c> is <c>true</c> the call resolved an idempotency-key
/// retry: the original patch is returned without re-emitting the proposed event
/// (otherwise UI / realtime fanout would fire twice for the same logical action).
/// </summary>
public sealed record CreatePromptPartPatchOutcome(PromptPartPatch Patch, bool IsExisting = false) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => IsExisting
        ? Array.Empty<IDomainEvent>()
        : [new PromptPartPatchProposed(Patch)];
}

/// <summary>
/// Persistence result of <see cref="IPromptPartPatchRepository.ApplyAsync"/>.
/// </summary>
public abstract record ApplyPromptPartPatchPersistenceOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Applied(PromptPartPatch Patch) : ApplyPromptPartPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new PromptPartPatchApplied(Patch)];
    }

    public sealed record NotFound : ApplyPromptPartPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }

    /// <summary>
    /// Patch is no longer in <c>proposed</c>; concurrent apply / reject won the
    /// race. Carries the latest snapshot so callers can surface it back.
    /// </summary>
    public sealed record AlreadyDecided(PromptPartPatch Patch) : ApplyPromptPartPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }
}

public abstract record RejectPromptPartPatchPersistenceOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Rejected(PromptPartPatch Patch) : RejectPromptPartPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new PromptPartPatchRejected(Patch)];
    }

    public sealed record NotFound : RejectPromptPartPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }

    public sealed record AlreadyDecided(PromptPartPatch Patch) : RejectPromptPartPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }
}
