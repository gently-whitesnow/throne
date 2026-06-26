namespace Throne.Infrastructure.EfCore.Rows;

internal sealed class IntentPinRow
{
    public string Id { get; set; } = string.Empty;
    public string IntentId { get; set; } = string.Empty;
    public string ContextTagId { get; set; } = string.Empty;
    public string PinSortKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
