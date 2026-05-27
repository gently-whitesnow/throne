using Microsoft.Extensions.Options;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// In-process TTL-cache over <see cref="ICapabilityProbe"/>. Singleton-scoped — capability
/// detection has no per-request state. A simple per-name lock guards against thundering
/// herd when the TTL expires under concurrent <c>GET /settings/capabilities</c> calls.
/// </summary>
internal sealed class CapabilityDetectionCache(
    IEnumerable<ICapabilityProbe> probes,
    IOptions<CapabilityDetectionOptions> options,
    TimeProvider clock) : ICapabilityDetectionCache
{
    private readonly Dictionary<string, ICapabilityProbe> _probes =
        probes.ToDictionary(p => p.CapabilityName, p => p, StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly object _registryLock = new();

    public async Task<CapabilityProbeResult?> GetAsync(string capabilityName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);
        if (!_probes.TryGetValue(capabilityName, out var probe))
        {
            return null;
        }

        var ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.DetectionTtlSeconds));
        var now = clock.GetUtcNow();

        if (TryGetFresh(capabilityName, now, ttl, out var cached))
        {
            return cached;
        }

        var gate = AcquireGate(capabilityName);
        await gate.WaitAsync(ct);
        try
        {
            // Re-check inside the gate to fold simultaneous misses.
            if (TryGetFresh(capabilityName, clock.GetUtcNow(), ttl, out cached))
            {
                return cached;
            }

            var result = await probe.ProbeAsync(ct);
            lock (_registryLock)
            {
                _entries[capabilityName] = new Entry(result, clock.GetUtcNow());
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetFresh(string name, DateTimeOffset now, TimeSpan ttl, out CapabilityProbeResult? result)
    {
        lock (_registryLock)
        {
            if (_entries.TryGetValue(name, out var entry) && now - entry.At < ttl)
            {
                result = entry.Result;
                return true;
            }
        }
        result = null;
        return false;
    }

    private SemaphoreSlim AcquireGate(string name)
    {
        lock (_registryLock)
        {
            if (!_locks.TryGetValue(name, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _locks[name] = gate;
            }
            return gate;
        }
    }

    private readonly record struct Entry(CapabilityProbeResult Result, DateTimeOffset At);
}
