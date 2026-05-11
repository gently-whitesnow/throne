using Throne.Application.Events;
using Throne.Domain.Instructions;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence result of <see cref="IInstructionPatchRepository.CreateAsync"/>.
/// Carries a <c>InstructionPatchProposed</c> domain event for fresh inserts.
/// When <c>IsExisting</c> is <c>true</c> the call resolved an idempotency-key
/// retry: the original patch is returned without re-emitting the proposed event
/// (otherwise UI / realtime fanout would fire twice for the same logical action).
/// </summary>
public sealed record CreateInstructionPatchOutcome(InstructionPatch Patch, bool IsExisting = false) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => IsExisting
        ? Array.Empty<IDomainEvent>()
        : [new InstructionPatchProposed(Patch)];
}

/// <summary>
/// Persistence result of <see cref="IInstructionPatchRepository.ApplyAsync"/>.
/// Apply emits one of the two terminal events depending on whether the operator
/// edited the patch text before applying.
/// </summary>
public abstract record ApplyInstructionPatchPersistenceOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Applied(InstructionPatch Patch) : ApplyInstructionPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Patch.Status switch
        {
            InstructionPatchStatusNames.AppliedEdited => [new InstructionPatchApplied(Patch)],
            _ => [new InstructionPatchApplied(Patch)],
        };
    }

    public sealed record NotFound : ApplyInstructionPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }

    /// <summary>
    /// Patch is no longer in <c>proposed</c>; concurrent apply / reject won the
    /// race. Carries the latest snapshot so callers can surface it back.
    /// </summary>
    public sealed record AlreadyDecided(InstructionPatch Patch) : ApplyInstructionPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }
}

public abstract record RejectInstructionPatchPersistenceOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Rejected(InstructionPatch Patch) : RejectInstructionPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new InstructionPatchRejected(Patch)];
    }

    public sealed record NotFound : RejectInstructionPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }

    public sealed record AlreadyDecided(InstructionPatch Patch) : RejectInstructionPatchPersistenceOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }
}
