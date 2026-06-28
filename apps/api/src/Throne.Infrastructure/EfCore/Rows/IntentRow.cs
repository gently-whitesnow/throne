namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>intents</c> table. Mirrors <c>IntentDocument</c> field-for-
/// field; <c>tag_ids</c> is materialized as a JSON array column so the row stays a single
/// SQLite tuple without a join table.
/// </summary>
internal sealed class IntentRow
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public List<string> TagIds { get; set; } = [];
    public string SortKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool CleanupLocalStateOnDone { get; set; } = true;
}
