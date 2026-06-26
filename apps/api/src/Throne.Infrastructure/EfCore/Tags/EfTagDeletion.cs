using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Tags;

/// <summary>
/// Two operations that span tags + intents and so live outside the lifecycle file:
/// <list type="bullet">
/// <item><see cref="CountAttachedIntentsAsync"/> — JSON1 count of intents whose
/// <c>tag_ids</c> array contains the given id. Robust against future serializer changes
/// because it parses the JSON array rather than matching the raw text.</item>
/// <item><see cref="DeleteAsync"/> — load every intent currently referencing the tag,
/// mutate the JSON array in-memory, UPDATE per row, then drop the tag. The whole flow
/// runs under the ambient UoW so the SQLite transaction commits atomically.</item>
/// </list>
/// </summary>
internal sealed class EfTagDeletion(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    private const string CountAttachedSql =
        "SELECT COUNT(*) AS Value FROM " + EfTableNames.Intents.Table + " i "
        + "WHERE EXISTS (SELECT 1 FROM json_each(i.tag_ids) WHERE value = {0})";

    public Task<int> CountAttachedIntentsAsync(TagId id, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var wire = id.Value;
            // SQLite JSON1: count intents whose tag_ids array contains the id. EXISTS +
            // json_each walks the array regardless of formatting (quotes, spaces, etc.) and
            // stays robust against future serializer changes — unlike a LIKE-on-JSON probe.
            var counts = await ctx.Database
                .SqlQueryRaw<int>(CountAttachedSql, wire)
                .ToListAsync(c);
            return counts.Count == 0 ? 0 : counts[0];
        }, ct);

    public async Task<DeleteTagOutcome> DeleteAsync(TagId id, DateTimeOffset now, CancellationToken ct)
    {
        var ctx = RequireContext(nameof(DeleteAsync));
        var wire = id.Value;

        var existing = await ctx.Set<TagRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (existing is null)
        {
            return new DeleteTagOutcome.NotFound();
        }

        // Find every intent referencing this tag. JSON1 keeps the probe robust against future
        // serializer-formatting changes (whitespace etc.) — we cannot rely on a LIKE pattern.
        var probe = $"%\"{wire}\"%";
        var attachedRows = await ctx.Set<IntentRow>()
            .Where(r => EF.Functions.Like(EF.Property<string>(r, "tag_ids"), probe))
            .ToListAsync(ct);

        // Detach + apply the JSON mutation per row so each UPDATE goes through EF (the JSON
        // converter re-serializes List<string> back to canonical form). One bulk SQL would skip
        // the converter and risk drift.
        var detached = new List<Intent>(attachedRows.Count);
        foreach (var row in attachedRows)
        {
            ctx.Entry(row).State = EntityState.Detached;
            if (!row.TagIds.Contains(wire))
            {
                // LIKE may yield false positives on substring tag ids — skip those.
                continue;
            }

            var newTagIds = row.TagIds.Where(t => !string.Equals(t, wire, StringComparison.Ordinal)).ToList();
            var newUpdatedAt = now;
            await ctx.Set<IntentRow>()
                .Where(r => r.Id == row.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.TagIds, newTagIds)
                    .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

            // Build the post-detach domain snapshot from the row we already loaded.
            row.TagIds = newTagIds;
            row.UpdatedAt = newUpdatedAt;
            detached.Add(IntentRowMapper.ToDomain(row));
        }

        var deletedTag = await ctx.Set<TagRow>()
            .Where(r => r.Id == wire)
            .ExecuteDeleteAsync(ct);
        if (deletedTag == 0)
        {
            return new DeleteTagOutcome.NotFound();
        }

        return new DeleteTagOutcome.Deleted(id, detached);
    }

    private ThroneDbContext RequireContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfTagDeletion.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
