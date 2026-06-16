using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

public sealed class TerminalHookStatusAck(
    TerminalHookStatusHandler hookStatus,
    UserPromptSubmitHookContextHandler userPromptContext,
    ILogger<TerminalHookStatusAck> logger
)
{
    public async Task<TerminalHookCallbackResponse> HandleAsync(
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
        }
        catch (ApiException ex)
        {
            TerminalEndpointLog.HookStatusFailed(logger, intentId, @event, ex);
        }

        return new TerminalHookCallbackResponse
        {
            HookSpecificOutput = await BuildHookSpecificOutputAsync(intentId, @event, ct),
        };
    }

    private async Task<TerminalUserPromptSubmitHookOutput?> BuildHookSpecificOutputAsync(
        string intentId,
        Event @event,
        CancellationToken ct
    )
    {
        if (@event is not Event.UserPromptSubmit)
        {
            return null;
        }

        var context = await userPromptContext.BuildAsync(intentId, ct);
        if (string.IsNullOrEmpty(context))
        {
            return null;
        }

        return new TerminalUserPromptSubmitHookOutput
        {
            HookEventName = TerminalUserPromptSubmitHookOutputHookEventName.UserPromptSubmit,
            AdditionalContext = context,
        };
    }

    private static string ToHookEvent(Event @event) =>
        @event switch
        {
            Event.Stop => TerminalHookEvents.Stop,
            Event.UserPromptSubmit => TerminalHookEvents.UserPromptSubmit,
            Event.Notification => TerminalHookEvents.Notification,
            Event.PostToolUse => TerminalHookEvents.PostToolUse,
            _ => throw new ArgumentOutOfRangeException(
                nameof(@event),
                $"Unknown terminal hook event '{@event}'."
            ),
        };
}
