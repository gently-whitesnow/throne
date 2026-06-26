namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>intent_attachments</c> table. The BLOB payload lives in
/// <see cref="ContentBytes"/>. When the compression worker downsamples an image we
/// overwrite the BLOB in place and flip <see cref="CompressionState"/> to <c>ready</c>;
/// the pre-compression bytes are not retained.
/// </summary>
internal sealed class IntentAttachmentRow
{
    public string Id { get; set; } = string.Empty;
    public string IntentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CompressionState { get; set; }
    public int? DerivedWidth { get; set; }
    public int? DerivedHeight { get; set; }
    public byte[] ContentBytes { get; set; } = [];
}
