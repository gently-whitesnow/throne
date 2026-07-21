namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>intent_card_attachments</c> table (ADR-0052). Mirrors
/// <c>IntentCardAttachmentSnapshot</c>: identity (<c>id</c> + <c>(intent_id, tracker, board_id, card_id)</c>
/// coordinate) plus the last-pulled snapshot and its recorded availability.
/// </summary>
internal sealed class IntentCardAttachmentRow
{
    public string Id { get; set; } = string.Empty;
    public string IntentId { get; set; } = string.Empty;
    public string Tracker { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? ColumnTitle { get; set; }
    public bool Archived { get; set; }
    public string? CardVersion { get; set; }
    public string Availability { get; set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
