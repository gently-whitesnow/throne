using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Seeds a single vendor's per-directory trust store. One implementation per agent CLI;
/// <see cref="WorkspaceTrust"/> picks the right one by <see cref="Vendor"/>. May throw on I/O —
/// the dispatcher owns the best-effort swallow so every seeder stays a thin read-modify-write.
/// </summary>
internal interface IWorkspaceTrustSeeder
{
    string Vendor { get; }

    void Seed(string workspacePath);

    /// <summary>
    /// Inverse of <see cref="Seed"/>: drop every trust entry whose path lies at or under
    /// <paramref name="directoryPrefix"/>. May throw on I/O — the dispatcher owns the swallow.
    /// </summary>
    void RemoveTrustedUnder(string directoryPrefix);
}

/// <summary>
/// Vendor-neutral entry point for trust seeding: dispatches to the matching
/// <see cref="IWorkspaceTrustSeeder"/> and enforces the best-effort contract — an unknown vendor
/// is a no-op and an I/O / parse failure is logged but never aborts the spawn.
/// </summary>
internal sealed class WorkspaceTrust(
    IEnumerable<IWorkspaceTrustSeeder> seeders,
    ILogger<WorkspaceTrust> log) : IWorkspaceTrust
{
    private readonly Dictionary<string, IWorkspaceTrustSeeder> _seeders =
        seeders.ToDictionary(s => s.Vendor, StringComparer.Ordinal);

    public Task EnsureTrustedAsync(string vendor, string workspacePath, CancellationToken ct)
    {
        if (!_seeders.TryGetValue(vendor, out var seeder))
        {
            return Task.CompletedTask;
        }

        try
        {
            seeder.Seed(workspacePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A write failure just means the operator confirms the prompt once, as before.
            TerminalsLog.WorkspaceTrustSeedFailed(log, vendor, workspacePath, ex.Message);
        }

        return Task.CompletedTask;
    }

    public Task RemoveTrustedUnderAsync(string directoryPrefix, CancellationToken ct)
    {
        // Clean every vendor unconditionally: which CLI actually ran for this intent is not
        // tracked, and an absent file / entry is a no-op inside the pure transform.
        foreach (var seeder in _seeders.Values)
        {
            try
            {
                seeder.RemoveTrustedUnder(directoryPrefix);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TerminalsLog.WorkspaceUntrustFailed(log, seeder.Vendor, directoryPrefix, ex.Message);
            }
        }

        return Task.CompletedTask;
    }
}
