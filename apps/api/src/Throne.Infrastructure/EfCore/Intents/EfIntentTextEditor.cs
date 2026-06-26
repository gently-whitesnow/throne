using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal sealed class EfIntentTextEditor(EfSessionAccessor sessions, IIntentEventRepository intentEvents)
{
    public async Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var ctx = RequireContext(nameof(ReplaceTextAsync));
        var wire = id.Value;

        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new ReplaceIntentTextOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new ReplaceIntentTextOutcome.VersionConflict(row.CurrentVersion);
        }

        // Detach so domain mutation (and subsequent ExecuteUpdate) doesn't trigger an EF
        // change-tracked save with a stale concurrency token.
        ctx.Entry(row).State = EntityState.Detached;

        var intent = IntentRowMapper.ToDomain(row);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.ReplaceText(oldText, newText, newVersionId, now, changedBy);

        return domainResult switch
        {
            ReplaceTextResult.MatchNotFound m => new ReplaceIntentTextOutcome.MatchNotFound(m.QueryPreview),
            ReplaceTextResult.MatchAmbiguous a => new ReplaceIntentTextOutcome.MatchAmbiguous(a.MatchesCount, a.MatchLines),
            ReplaceTextResult.Replaced replaced => await PersistAsync(ctx, intent, expectedVersion, replaced, ct),
            _ => throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}"),
        };
    }

    public async Task<InsertIntentTextAfterLineOutcome> InsertTextAfterLineAsync(
        IntentId id,
        int expectedVersion,
        int afterLine,
        string insertText,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(insertText);

        var ctx = RequireContext(nameof(InsertTextAfterLineAsync));
        var wire = id.Value;

        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new InsertIntentTextAfterLineOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new InsertIntentTextAfterLineOutcome.VersionConflict(row.CurrentVersion);
        }

        ctx.Entry(row).State = EntityState.Detached;

        var intent = IntentRowMapper.ToDomain(row);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.InsertTextAfterLine(afterLine, insertText, newVersionId, now, TextVersionAuthor.Agent);

        return domainResult switch
        {
            InsertTextResult.LineOutOfRange r => new InsertIntentTextAfterLineOutcome.LineOutOfRange(r.TotalLines, r.RequestedAfterLine),
            InsertTextResult.Inserted inserted => await PersistAsync(ctx, intent, expectedVersion, inserted, ct),
            _ => throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}"),
        };
    }

    private async Task<ReplaceIntentTextOutcome> PersistAsync(
        ThroneDbContext ctx,
        Intent intent,
        int expectedVersion,
        ReplaceTextResult.Replaced replaced,
        CancellationToken ct)
    {
        var wire = intent.Id.Value;
        var newText = intent.State.Text;
        var newVersion = intent.State.CurrentVersion;
        var newUpdatedAt = intent.State.UpdatedAt;

        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Text, newText)
                .SetProperty(r => r.CurrentVersion, newVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return new ReplaceIntentTextOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
        }

        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                replaced.Version,
                replaced.Version.ChangedAt),
            ct);
        return new ReplaceIntentTextOutcome.Replaced(intent);
    }

    private async Task<InsertIntentTextAfterLineOutcome> PersistAsync(
        ThroneDbContext ctx,
        Intent intent,
        int expectedVersion,
        InsertTextResult.Inserted inserted,
        CancellationToken ct)
    {
        var wire = intent.Id.Value;
        var newText = intent.State.Text;
        var newVersion = intent.State.CurrentVersion;
        var newUpdatedAt = intent.State.UpdatedAt;

        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Text, newText)
                .SetProperty(r => r.CurrentVersion, newVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return new InsertIntentTextAfterLineOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
        }

        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                inserted.Version,
                inserted.Version.ChangedAt),
            ct);
        return new InsertIntentTextAfterLineOutcome.Inserted(intent);
    }

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentTextEditor.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
