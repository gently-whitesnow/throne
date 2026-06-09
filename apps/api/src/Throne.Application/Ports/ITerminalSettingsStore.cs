namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for the single operator-controlled terminal setting: the default
/// agent vendor used to pre-fill the launch controls and as the server-side fallback
/// when a run/restart request omits <c>vendor</c>. Model and effort defaults are native
/// to the vendor and never persisted.
/// </summary>
public interface ITerminalSettingsStore
{
    /// <summary>
    /// Read the persisted default vendor. Returns the native default
    /// (<see cref="Throne.Application.Terminals.TerminalAgentCatalog.DefaultVendor"/>)
    /// when nothing was ever written, so callers never special-case a missing row.
    /// </summary>
    Task<string> GetDefaultVendorAsync(CancellationToken ct);

    /// <summary>Upsert the default vendor. <paramref name="vendor"/> is pre-validated.</summary>
    Task SetDefaultVendorAsync(string vendor, CancellationToken ct);
}
