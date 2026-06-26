using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Training;
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
        var row = await LoadDetachedRowAsync(ctx, id, ct);
        if (row is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }

        var intent = IntentRowMapper.ToDomain(row);
        var originalVersion = intent.State.CurrentVersion;
        var originalStatus = intent.State.Status;

        var textVersion = ApplyOptionalAppend(intent, appendText, changedBy, now);
        var statusChanged = intent.SetStatus(status, now);
        if (!statusChanged && textVersion is null)
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        var conflict = await ApplyStatusCasAsync(ctx, id, intent, originalVersion, originalStatus, withTextUpdate: true, ct);
        if (conflict is not null)
        {
            return conflict;
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
            await RecordStatusChangeAsync(ctx, id, intent, originalStatus, changedBy, source, reason, now, ct);
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
        var row = await LoadDetachedRowAsync(ctx, id, ct);
        if (row is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }

        var intent = IntentRowMapper.ToDomain(row);
        var originalStatus = intent.State.Status;
        var originalVersion = intent.State.CurrentVersion;
        if (!intent.SetStatus(status, now))
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        var conflict = await ApplyStatusCasAsync(ctx, id, intent, originalVersion, originalStatus, withTextUpdate: false, ct);
        if (conflict is not null)
        {
            return conflict;
        }

        await RecordStatusChangeAsync(ctx, id, intent, originalStatus, IntentTrainingAuthor.System, source, reason, now, ct);
        return new SetIntentStatusOutcome.Updated(intent);
    }

    private static async Task<IntentRow?> LoadDetachedRowAsync(ThroneDbContext ctx, IntentId id, CancellationToken ct)
    {
        var wire = id.Value;
        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is not null)
        {
            ctx.Entry(row).State = EntityState.Detached;
        }
        return row;
    }

    // CAS: filter on both (current_version, status) — and accept empty Status when the mapper
    // normalised "" → "draft" so legacy rows still update. Returns the outcome envelope on
    // conflict / disappearance, or null when the update succeeded.
    private static async Task<SetIntentStatusOutcome?> ApplyStatusCasAsync(
        ThroneDbContext ctx,
        IntentId id,
        Intent intent,
        int originalVersion,
        string originalStatus,
        bool withTextUpdate,
        CancellationToken ct)
    {
        var wire = id.Value;
        var newText = intent.State.Text;
        var newStatus = intent.State.Status;
        var newVersion = intent.State.CurrentVersion;
        var newUpdatedAt = intent.State.UpdatedAt;
        var includeEmptyStatus = string.Equals(originalStatus, IntentStatusNames.Draft, StringComparison.Ordinal);

        var query = ctx.Set<IntentRow>()
            .Where(r => r.Id == wire
                && r.CurrentVersion == originalVersion
                && (r.Status == originalStatus || (includeEmptyStatus && r.Status == string.Empty)));
        var affected = withTextUpdate
            ? await query.ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Text, newText)
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.CurrentVersion, newVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct)
            : await query.ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, newStatus)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected != 0)
        {
            return null;
        }
        var fresh = await ctx.Set<IntentRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == wire, ct);
        return fresh is null
            ? new SetIntentStatusOutcome.NotFound()
            : new SetIntentStatusOutcome.Conflict(fresh.CurrentVersion, fresh.Status);
    }

    private static async Task RecordStatusChangeAsync(
        ThroneDbContext ctx,
        IntentId id,
        Intent intent,
        string originalStatus,
        IntentTrainingAuthor changedBy,
        string source,
        string? reason,
        DateTimeOffset now,
        CancellationToken ct)
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
