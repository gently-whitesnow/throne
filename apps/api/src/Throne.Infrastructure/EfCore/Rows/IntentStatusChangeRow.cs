namespace Throne.Infrastructure.EfCore.Rows;

internal sealed class IntentStatusChangeRow
{
    public string Id { get; set; } = string.Empty;
    public string IntentId { get; set; } = string.Empty;
    public int IntentVersionAtWrite { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
