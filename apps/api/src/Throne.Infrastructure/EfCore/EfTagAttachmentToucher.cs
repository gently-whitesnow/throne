using Microsoft.EntityFrameworkCore;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Bumps <c>tags.last_attached_at</c> for the supplied tag ids inside the current
/// context. Plain SQL keeps the toucher independent of the Tag cluster's row /
/// configuration that lands in a later slice — the column shape is part of the
/// shared schema contract, not the Tag aggregate's private surface.
/// </summary>
internal static class EfTagAttachmentToucher
{
    public static async Task TouchAsync(
        ThroneDbContext context,
        IReadOnlyCollection<string> tagIds,
        DateTimeOffset attachedAt,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (tagIds is null || tagIds.Count == 0)
        {
            return;
        }

        var ids = tagIds.ToArray();
        var table = EfTableNames.Tags.Table;
        // Parameter array must be expanded into N positional placeholders; FormattableString
        // lets EF SQL-parameterize the timestamp + the id list safely.
        var placeholders = string.Join(",", Enumerable.Range(0, ids.Length).Select(i => $"{{{i + 1}}}"));
        var sql = $"UPDATE {table} SET last_attached_at = {{0}} WHERE id IN ({placeholders})";
        var args = new object[ids.Length + 1];
        args[0] = attachedAt;
        Array.Copy(ids, 0, args, 1, ids.Length);
        await context.Database.ExecuteSqlRawAsync(sql, args, ct);
    }
}
