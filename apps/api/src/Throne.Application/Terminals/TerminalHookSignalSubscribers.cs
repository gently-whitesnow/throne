namespace Throne.Application.Terminals;

public sealed class TerminalReadinessSignalSubscriber(TerminalReadinessSignals signals)
    : ITerminalHookSubscriber
{
    public Task HandleAsync(TerminalHookEvent hook, CancellationToken ct)
    {
        if (hook.Event == TerminalHookEvents.SessionReady)
        {
            signals.TrySignal(hook.IntentId);
        }

        return Task.CompletedTask;
    }
}

public sealed class TerminalPromptSubmitSignalSubscriber(TerminalPromptSubmitSignals signals)
    : ITerminalHookSubscriber
{
    public Task HandleAsync(TerminalHookEvent hook, CancellationToken ct)
    {
        if (hook.Event == TerminalHookEvents.UserPromptSubmit)
        {
            signals.TrySignal(hook.IntentId);
        }

        return Task.CompletedTask;
    }
}
