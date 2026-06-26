namespace Throne.Infrastructure.EfCore.Rows;

internal sealed class IntentLinkRow
{
    public string Id { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public bool Blocking { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
