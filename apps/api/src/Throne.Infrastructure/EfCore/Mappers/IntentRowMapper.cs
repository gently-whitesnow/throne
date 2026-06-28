using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentRowMapper
{
    public static IntentRow ToRow(Intent intent) => new()
    {
        Id = intent.Id.Value,
        Title = intent.State.Title,
        Text = intent.State.Text,
        Status = intent.State.Status,
        CurrentVersion = intent.State.CurrentVersion,
        TagIds = intent.TagIds.Select(t => t.Value).ToList(),
        SortKey = intent.State.SortKey,
        CreatedAt = intent.CreatedAt,
        UpdatedAt = intent.State.UpdatedAt,
        CleanupLocalStateOnDone = intent.State.CleanupLocalStateOnClose,
    };

    public static Intent ToDomain(IntentRow row) => Intent.Restore(
        id: new IntentId(row.Id),
        text: row.Text,
        status: string.IsNullOrWhiteSpace(row.Status) ? IntentStatusNames.Draft : row.Status,
        currentVersion: row.CurrentVersion,
        tagIds: row.TagIds.Select(v => new TagId(v)).ToList(),
        createdAt: row.CreatedAt,
        updatedAt: row.UpdatedAt,
        sortKey: row.SortKey,
        cleanupLocalStateOnDone: row.CleanupLocalStateOnDone,
        title: row.Title);
}
