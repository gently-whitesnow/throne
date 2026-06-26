using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal sealed class EfIntentStatusMutator(EfSessionAccessor sessions, IIntentEventRepository intentEvents)
{
    public async Task<SetIntentStatusOutcome> SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        string? reason,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var ctx = RequireContext(nameof(SetStatusAsync));
        var wire = id.Value;

        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }
        ctx.Entry(row).State = EntityState.Detached;

        var intent = IntentRowMapper.ToDomain(row);
        var originalVersion = intent.State.CurrentVersion;
        var originalStatus = intent.State.Status;

        var textVersion = ApplyOptionalAppend(intent, appendText, changedBy, now);
        var statusChanged = intent.SetStatus(status, now);
        if (!statusChanged && textVersion is null)
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        // CAS: filter on both (current_version, status) — and accept empty Status when
        // mapper normalised "" → "draft" so legacy rows still update.
        var newText = intent.State.Text;
        var newStatus = intent.State.Status;
        var newVersion = intent.State.CurrentVersion;
        var newUpdatedAt = intent.State.UpdatedAt;
        var includeEmptyStatus = string.Equals(originalStatus, IntentStatusNames.Draft, StringComparison.Ordinal);

        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire
                && r.CurrentVersion == originalVersion
                && (r.Status == originalStatus || (includeEmptyStatus && r.Status == string.Empty)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Text, newText)
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.CurrentVersion, newVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new SetIntentStatusOutcome.NotFound()
                : new SetIntentStatusOutcome.Conflict(fresh.CurrentVersion, fresh.Status);
        }

        if (textVersion is not null)
        {
            await intentEvents.AppendAsync(
                IntentEvent.ForText(
                    Guid.NewGuid().ToString("N"),
                    intent.Id,
                    textVersion,
                    textVersion.ChangedAt),
                ct);
        }

        if (statusChanged)
        {
            var statusChange = IntentStatusChange.Create(
                id: Guid.NewGuid().ToString("N"),
                intentId: id,
                intentVersionAtWrite: intent.State.CurrentVersion,
                fromStatus: originalStatus,
                toStatus: intent.State.Status,
                source: source,
                createdAt: now,
                createdBy: changedBy,
                reason: reason);
            ctx.Set<IntentStatusChangeRow>().Add(IntentStatusChangeRowMapper.ToRow(statusChange));
            await ctx.SaveChangesAsync(ct);
        }

        return new SetIntentStatusOutcome.Updated(intent);
    }

    public async Task<SetIntentStatusOutcome> SetStatusBySystemAsync(
        IntentId id,
        string status,
        string? reason,
        string source,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var ctx = RequireContext(nameof(SetStatusBySystemAsync));
        var wire = id.Value;

        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }
        ctx.Entry(row).State = EntityState.Detached;

        var intent = IntentRowMapper.ToDomain(row);
        var originalStatus = intent.State.Status;
        var originalVersion = intent.State.CurrentVersion;
        if (!intent.SetStatus(status, now))
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        var newStatus = intent.State.Status;
        var newUpdatedAt = intent.State.UpdatedAt;
        var includeEmptyStatus = string.Equals(originalStatus, IntentStatusNames.Draft, StringComparison.Ordinal);

        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire
                && r.CurrentVersion == originalVersion
                && (r.Status == originalStatus || (includeEmptyStatus && r.Status == string.Empty)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new SetIntentStatusOutcome.NotFound()
                : new SetIntentStatusOutcome.Conflict(fresh.CurrentVersion, fresh.Status);
        }

        var statusChange = IntentStatusChange.Create(
            id: Guid.NewGuid().ToString("N"),
            intentId: id,
            intentVersionAtWrite: intent.State.CurrentVersion,
            fromStatus: originalStatus,
            toStatus: intent.State.Status,
            source: source,
            createdAt: now,
            createdBy: IntentTrainingAuthor.System,
            reason: reason);
        ctx.Set<IntentStatusChangeRow>().Add(IntentStatusChangeRowMapper.ToRow(statusChange));
        await ctx.SaveChangesAsync(ct);

        return new SetIntentStatusOutcome.Updated(intent);
    }

    public async Task<SetIntentTagsOutcome> SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tagIds);

        var ctx = RequireContext(nameof(SetTagsAsync));
        var wire = id.Value;

        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new SetIntentTagsOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new SetIntentTagsOutcome.VersionConflict(row.CurrentVersion);
        }
        ctx.Entry(row).State = EntityState.Detached;

        var intent = IntentRowMapper.ToDomain(row);
        var oldTagIds = row.TagIds;
        var changed = intent.SetTags(tagIds, now);
        if (!changed)
        {
            return new SetIntentTagsOutcome.Updated(intent, Changed: false);
        }

        var newTagIds = intent.TagIds.Select(t => t.Value).ToList();
        var newUpdatedAt = intent.State.UpdatedAt;

        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.TagIds, newTagIds)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new SetIntentTagsOutcome.NotFound()
                : new SetIntentTagsOutcome.VersionConflict(fresh.CurrentVersion);
        }

        var added = newTagIds.Where(t => !oldTagIds.Contains(t)).ToList();
        await EfTagAttachmentToucher.TouchAsync(ctx, added, now, ct);

        return new SetIntentTagsOutcome.Updated(intent, Changed: true);
    }

    public async Task SetCleanupLocalStateOnDoneAsync(
        IntentId id,
        bool value,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ctx = RequireContext(nameof(SetCleanupLocalStateOnDoneAsync));
        var wire = id.Value;
        await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CleanupLocalStateOnDone, value)
                .SetProperty(r => r.UpdatedAt, now), ct);
    }

    private static TextVersion? ApplyOptionalAppend(
        Intent intent,
        string? appendText,
        IntentTrainingAuthor changedBy,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(appendText))
        {
            return null;
        }
        var appendResult = intent.AppendText(
            appendText,
            Guid.NewGuid().ToString("N"),
            now,
            ToTextVersionAuthor(changedBy));
        if (appendResult is not InsertTextResult.Inserted inserted)
        {
            throw new InvalidOperationException($"Unexpected append result: {appendResult.GetType().Name}");
        }
        return inserted.Version;
    }

    private static TextVersionAuthor ToTextVersionAuthor(IntentTrainingAuthor author) => author switch
    {
        IntentTrainingAuthor.User => TextVersionAuthor.User,
        IntentTrainingAuthor.Agent => TextVersionAuthor.Agent,
        IntentTrainingAuthor.System => TextVersionAuthor.System,
        _ => throw new InvalidOperationException($"Unknown training author: {author}."),
    };

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentStatusMutator.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
