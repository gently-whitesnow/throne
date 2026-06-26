namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>dream_sessions</c> table. Mirrors
/// <c>DreamSessionDocument</c> field-for-field; the two opaque-id lists
/// (<c>processed_conversation_ids</c>, <c>proposed_patch_ids</c>) are JSON columns
/// since the entries are short, replace-only and never queried individually.
/// </summary>
internal sealed class DreamSessionRow
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string Vendor { get; set; } = string.Empty;
    public string? Host { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public List<string> ProcessedConversationIds { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string? Reflection { get; set; }
    public List<string> ProposedPatchIds { get; set; } = [];
}
