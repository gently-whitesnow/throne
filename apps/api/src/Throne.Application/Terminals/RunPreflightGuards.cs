using Throne.Application.Ports;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Validation helpers used by <see cref="RunPreflightOrchestrator"/>: capability gate,
/// intent existence, session-slot. Split out so the orchestrator stays within the
/// project-wide CA1502 type-level cyclomatic budget.
/// </summary>
public sealed class RunPreflightGuards(
    IIntentRepository intents,
    ICapabilitiesRepository capabilities,
    RunPreflightSpawn spawner)
{
    public async Task EnsureCapabilityEnabledAsync(CancellationToken ct)
    {
        var stored = await capabilities.GetAsync(ct);
        if (stored is null || !stored.IsEnabled(CapabilityNames.Terminal))
        {
            throw TerminalFailures.CapabilityDisabled(CapabilityNames.Terminal);
        }
    }

    public async Task<Intent> LoadIntentAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        return await intents.GetByIdAsync(new IntentId(intentId), ct)
            ?? throw RunPreflightFailures.IntentNotFound(intentId);
    }

    public async Task EnsureSessionSlotAsync(
        string intentId,
        string sessionName,
        bool restart,
        CancellationToken ct)
    {
        if (restart)
        {
            await spawner.KillSessionAsync(intentId, ct);
            return;
        }

        if (await spawner.HasSessionAsync(intentId, ct))
        {
            throw TerminalFailures.SessionAlreadyRunning(intentId, sessionName);
        }
    }
}

internal static class RunPreflightModeGuard
{
    public static void EnsureKnown(string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        if (!TerminalRunModes.IsKnown(mode))
        {
            throw TerminalFailures.ModeInvalid(mode);
        }
    }
}
