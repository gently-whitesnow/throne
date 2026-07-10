using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals.Quotas;

/// <summary>
/// Shared cache + failure-isolation shell for per-vendor quota adapters (ADR-0054 §2).
/// Derivatives implement <see cref="ProbeAsync"/> — every thrown exception, including a missing
/// token file, is swallowed here and turned into <see langword="null"/>. The catalog mapper
/// reads the null and hides the block; the launch path is never blocked by an adapter fault.
/// </summary>
internal abstract class HttpVendorQuotaAdapterBase(ILogger logger, TimeProvider clock)
    : IVendorQuotaAdapter, IDisposable
{
    // 60s matches Codex CLI's own rate-limit poller cadence (codex-rs/tui/src/chatwidget.rs).
    // Fast enough that a mid-session refresh reflects reality; slow enough that a page reload
    // storm from a stale react-query cache does not hammer the vendor endpoint.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private static readonly Action<ILogger, string, string, Exception?> LogProbeFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1, nameof(HttpVendorQuotaAdapterBase)),
            "vendor quota probe for '{Vendor}' failed ({ExceptionType}); UI block will be hidden");

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private VendorQuotaSnapshot? _cached;

    public abstract string Vendor { get; }

    public async Task<VendorQuotaSnapshot?> ReadAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (now - _cachedAt < CacheTtl)
        {
            return _cached;
        }

        await _gate.WaitAsync(ct);
        try
        {
            now = clock.GetUtcNow();
            if (now - _cachedAt < CacheTtl)
            {
                return _cached;
            }

            VendorQuotaSnapshot? snapshot;
            try
            {
                snapshot = await ProbeAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogProbeFailed(logger, Vendor, ex.GetType().Name, ex);
                snapshot = null;
            }

            _cached = snapshot;
            _cachedAt = now;
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads the vendor's OAuth session, calls the undocumented usage endpoint, parses it.
    /// May throw — the base class turns the throw into a null snapshot and logs a warning.
    /// </summary>
    protected abstract Task<VendorQuotaSnapshot?> ProbeAsync(CancellationToken ct);

    protected static double ClampPercent(double raw) => Math.Clamp(raw, 0d, 100d);

    public void Dispose() => _gate.Dispose();
}
