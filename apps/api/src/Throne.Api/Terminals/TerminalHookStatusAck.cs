using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

public sealed class TerminalHookStatusAck(
    TerminalHookStatusHandler hookStatus,
    TerminalReadinessSignals readinessSignals,
    TerminalPromptSubmitSignals submitSignals,
    ILogger<TerminalHookStatusAck> logger
)
{
    public async Task HandleAsync(
        string intentId,
        Event @event,
        TerminalRunMode? mode,
        CancellationToken ct
    )
    {
        var domainMode = mode is null ? null : TerminalRunResponseMapper.ToDomainMode(mode.Value);
        try
        {
            await hookStatus.HandleAsync(intentId, ToHookEvent(@event), domainMode, ct);
            if (@event == Event.SessionReady)
            {
                readinessSignals.TrySignal(intentId);
            }
            else if (@event == Event.UserPromptSubmit)
            {
                // Authoritative confirmation that the agent accepted the (just-pasted) prompt — wakes
                // the post-spawn delivery's confirm gate. No-op if nothing is armed (later turns).
                submitSignals.TrySignal(intentId);
            }
        }
        catch (ApiException ex)
        {
            TerminalEndpointLog.HookStatusFailed(logger, intentId, @event, ex);
        }
    }

    private static string ToHookEvent(Event @event) =>
        @event switch
        {
            Event.Stop => TerminalHookEvents.Stop,
            Event.UserPromptSubmit => TerminalHookEvents.UserPromptSubmit,
            Event.SessionReady => TerminalHookEvents.SessionReady,
            Event.Notification => TerminalHookEvents.Notification,
            Event.PostToolUse => TerminalHookEvents.PostToolUse,
            _ => throw new ArgumentOutOfRangeException(
                nameof(@event),
                $"Unknown terminal hook event '{@event}'."
            ),
        };
}
