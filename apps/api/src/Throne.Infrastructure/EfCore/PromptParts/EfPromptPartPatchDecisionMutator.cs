using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.PromptParts;

/// <summary>
/// Apply / Reject CAS transitions. Each mutation filters on
/// <c>status='proposed'</c> so a concurrent decision cannot win a second time; on 0 affected
/// rows we re-fetch the row to disambiguate NotFound from AlreadyDecided.
/// </summary>
internal sealed class EfPromptPartPatchDecisionMutator(EfSessionAccessor sessions)
{
    public async Task<ApplyPromptPartPatchPersistenceOutcome> ApplyAsync(
        PromptPartPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ctx = RequireContext(nameof(ApplyAsync));
        var wire = patch.Identity.Id;

        var newStatus = patch.State.Status;
        var newAppliedText = patch.State.AppliedText;
        var newAppliedVersion = patch.State.AppliedVersion;
        var newUpdatedAt = patch.State.UpdatedAt;
        var newDecidedAt = patch.State.DecidedAt;

        var affected = await ctx.Set<PromptPartPatchRow>()
            .Where(r => r.Id == wire && r.Status == PromptPartPatchStatusNames.Proposed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.AppliedText, newAppliedText)
                .SetProperty(r => r.AppliedVersion, newAppliedVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt)
                .SetProperty(r => r.DecidedAt, newDecidedAt), ct);
        if (affected > 0)
        {
            return new ApplyPromptPartPatchPersistenceOutcome.Applied(patch);
        }

        var fresh = await ctx.Set<PromptPartPatchRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == wire, ct);
        return fresh is null
            ? new ApplyPromptPartPatchPersistenceOutcome.NotFound()
            : new ApplyPromptPartPatchPersistenceOutcome.AlreadyDecided(PromptPartPatchRowMapper.ToDomain(fresh));
    }

    public async Task<RejectPromptPartPatchPersistenceOutcome> RejectAsync(
        PromptPartPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ctx = RequireContext(nameof(RejectAsync));
        var wire = patch.Identity.Id;

        var newStatus = patch.State.Status;
        var newRejectComment = patch.State.RejectComment;
        var newUpdatedAt = patch.State.UpdatedAt;
        var newDecidedAt = patch.State.DecidedAt;

        var affected = await ctx.Set<PromptPartPatchRow>()
            .Where(r => r.Id == wire && r.Status == PromptPartPatchStatusNames.Proposed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.RejectComment, newRejectComment)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt)
                .SetProperty(r => r.DecidedAt, newDecidedAt), ct);
        if (affected > 0)
        {
            return new RejectPromptPartPatchPersistenceOutcome.Rejected(patch);
        }

        var fresh = await ctx.Set<PromptPartPatchRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == wire, ct);
        return fresh is null
            ? new RejectPromptPartPatchPersistenceOutcome.NotFound()
            : new RejectPromptPartPatchPersistenceOutcome.AlreadyDecided(PromptPartPatchRowMapper.ToDomain(fresh));
    }

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfPromptPartPatchDecisionMutator.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
