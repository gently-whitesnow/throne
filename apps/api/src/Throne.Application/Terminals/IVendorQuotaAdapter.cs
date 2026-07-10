namespace Throne.Application.Terminals;

/// <summary>
/// Best-effort per-vendor quota probe (ADR-0054): reads Pro/Max subscription usage from the
/// undocumented endpoint the vendor's own CLI already polls. Contract on implementations —
/// any error (missing token, expired session, 4xx/5xx, unexpected schema, network fail) is
/// swallowed and surfaces as <see langword="null"/>. The catalog mapper hides the UI block
/// for that vendor and does NOT block the launch — see ADR-0054 §2.
/// </summary>
public interface IVendorQuotaAdapter
{
    /// <summary>Vendor token (<see cref="TerminalAgentCatalog"/>) this adapter serves.</summary>
    string Vendor { get; }

    /// <summary>Current quota snapshot, or <see langword="null"/> when the source is unavailable.</summary>
    Task<VendorQuotaSnapshot?> ReadAsync(CancellationToken ct);
}

/// <summary>Provider-neutral quota snapshot surfaced to the wire.</summary>
/// <param name="FiveHour">Rolling 5-hour usage window. Always present when the snapshot is not null.</param>
/// <param name="SevenDay">Rolling 7-day usage window. Null when the vendor does not surface a weekly axis (or has not populated it yet for this account).</param>
/// <param name="CreditsBalance">Vendor-specific credits balance (Codex). Null for vendors that don't report credits.</param>
public sealed record VendorQuotaSnapshot(
    VendorQuotaWindow FiveHour,
    VendorQuotaWindow? SevenDay,
    double? CreditsBalance);

/// <param name="UsedPercent">0-100. Adapters clamp before returning.</param>
/// <param name="ResetsAt">ISO 8601 UTC. Null when the vendor does not report a reset stamp.</param>
public sealed record VendorQuotaWindow(double UsedPercent, string? ResetsAt);
