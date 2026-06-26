using Microsoft.EntityFrameworkCore;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Bumps <c>tags.last_attached_at</c> for the supplied tag ids inside the current
/// context.
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
        await context.Set<TagRow>()
            .Where(t => ids.Contains(t.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastAttachedAt, attachedAt), ct);
    }
}
