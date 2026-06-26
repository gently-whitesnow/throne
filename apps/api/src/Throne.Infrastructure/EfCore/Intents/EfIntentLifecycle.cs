using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore.Intents;

internal sealed class EfIntentLifecycle(EfSessionAccessor sessions, IIntentEventRepository intentEvents)
{
    public async Task<CreateIntentOutcome> CreateAsync(
        Intent intent,
        TextVersion initialVersion,
        IntentStatusChange initialStatusChange,
        IReadOnlyList<Tag> upsertedTags,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(initialVersion);
        ArgumentNullException.ThrowIfNull(initialStatusChange);
        ArgumentNullException.ThrowIfNull(upsertedTags);

        var ctx = RequireContext(nameof(CreateAsync));

        // Event first (caller's transactional invariant is that the
        // unified history exists before the aggregate is committed), aggregate, status change,
        // tag toucher.
        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                initialVersion,
                initialVersion.ChangedAt),
            ct);

        ctx.Set<IntentRow>().Add(IntentRowMapper.ToRow(intent));
        ctx.Set<IntentStatusChangeRow>().Add(IntentStatusChangeRowMapper.ToRow(initialStatusChange));
        await ctx.SaveChangesAsync(ct);

        await EfTagAttachmentToucher.TouchAsync(
            ctx,
            intent.TagIds.Select(t => t.Value).ToList(),
            intent.CreatedAt,
            ct);

        return new CreateIntentOutcome(intent, upsertedTags);
    }

    public async Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct)
    {
        var ctx = RequireContext(nameof(DeleteAsync));
        var wire = id.Value;

        var deleted = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            return new DeleteIntentOutcome.NotFound();
        }

        // Cascade events: unified history of the dead intent in both roles (subject + peer).
        await ctx.Set<IntentEventRow>()
            .Where(r => r.IntentId == wire || r.PeerIntentId == wire)
            .ExecuteDeleteAsync(ct);
        // Cascade incoming + outgoing edges so the graph never references a ghost intent.
        await ctx.Set<IntentLinkRow>()
            .Where(r => r.FromId == wire || r.ToId == wire)
            .ExecuteDeleteAsync(ct);

        return new DeleteIntentOutcome.Deleted(wire);
    }

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentLifecycle.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
