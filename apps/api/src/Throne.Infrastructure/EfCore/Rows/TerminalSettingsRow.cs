namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the singleton <c>terminal_settings</c> table (<c>id = "terminal"</c>).
/// Holds the operator-controlled default agent vendor. A separate table from
/// <c>capabilities</c> so each settings axis has its own row shape and migration story.
/// </summary>
internal sealed class TerminalSettingsRow
{
    public const string SingletonId = "terminal";

    public string Id { get; set; } = SingletonId;
    public string DefaultVendor { get; set; } = string.Empty;
}
