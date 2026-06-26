namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>capabilities</c> table. Singleton row keyed by
/// <c>Capabilities.SingletonId</c>; <c>selections</c> is a JSON map (capability →
/// provider). A separate table from <c>terminal_settings</c> keeps each settings axis
/// readable on its own.
/// </summary>
internal sealed class CapabilitiesRow
{
    public string Id { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Dictionary<string, string> Selections { get; set; } = [];
}
