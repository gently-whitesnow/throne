using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

public sealed class OpenInTerminalService(
    CapabilitiesPersistence capabilities,
    ICapabilityDetectionCache detection,
    ITerminalOpenerRegistry openers,
    IIntentRepository intents,
    ITmuxSessionManager tmux)
{
    public async Task<OpenInTerminalResult> OpenAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var intent = await intents.GetByIdAsync(new IntentId(intentId), ct)
            ?? throw TerminalOpenerFailures.IntentNotFound(intentId);

        var sessionName = TmuxSessionName.For(intent.Id.Value);
        if (!await tmux.HasSessionAsync(intent.Id.Value, ct))
        {
            throw TerminalOpenerFailures.SessionNotLive(intent.Id.Value, sessionName);
        }

        var opener = await ResolveOpenerAsync(ct);
        await tmux.StopPipeAsync(intent.Id.Value, ct);
        await opener.OpenAsync(intent.Id.Value, sessionName, ct);
        return new OpenInTerminalResult(opener.ProviderName, sessionName);
    }

    private async Task<ITerminalOpener> ResolveOpenerAsync(CancellationToken ct)
    {
        var stored = await capabilities.GetAsync(ct);
        var selected = stored?.GetSelectedProvider(CapabilityNames.OpenInTerminal);

        if (!string.IsNullOrWhiteSpace(selected))
        {
            var opener = openers.Find(selected)
                ?? throw TerminalOpenerFailures.ProviderUnavailable(selected, "not registered on this Throne build");
            var probe = await detection.GetAsync(opener.ProviderName, ct);
            if (probe is null || !probe.Detected)
            {
                throw TerminalOpenerFailures.ProviderUnavailable(opener.ProviderName, probe?.Detail);
            }
            return opener;
        }

        var detected = new List<ITerminalOpener>();
        foreach (var candidate in openers.All)
        {
            var probe = await detection.GetAsync(candidate.ProviderName, ct);
            if (probe is { Detected: true })
            {
                detected.Add(candidate);
            }
        }

        return detected.Count switch
        {
            0 => throw TerminalOpenerFailures.NoProviderAvailable(),
            1 => detected[0],
            _ => throw TerminalOpenerFailures.AmbiguousProvider(detected.Select(o => o.ProviderName).ToList()),
        };
    }
}
