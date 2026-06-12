namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral names of the agent runtime hook events the embedded terminal binds to the
/// local callback endpoint (ADR-0034 §4). Shared by the vendor adapters that inject the hooks and
/// the endpoint handler that derives intent status from them, so the wire string stays in one place.
/// </summary>
public static class TerminalHookEvents
{
    public const string Stop = "Stop";
    public const string UserPromptSubmit = "UserPromptSubmit";

    public static readonly IReadOnlyList<string> All = [Stop, UserPromptSubmit];
}
