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
    public const string SessionReady = "SessionReady";

    // A permission/approval prompt does NOT end the turn, so Claude Code fires no Stop while the
    // agent is blocked on it — only a Notification. Without this the intent never parks in
    // awaiting_operator for the (common) "agent is waiting for me to approve a tool" case.
    public const string Notification = "Notification";

    // Resume signal: approving a permission is not a UserPromptSubmit, so the post-approval tool run
    // (PostToolUse) is the first deterministic "agent is working again" event that returns the intent
    // to its spawn phase. Fires per tool call; the transition is idempotent in the domain.
    public const string PostToolUse = "PostToolUse";

    /// <summary>
    /// Every event the status handler recognises. Injection is vendor-specific (see
    /// <see cref="ClaudeBindings"/> / <see cref="CodexBindings"/>); this is the recognise-side set.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
        [Stop, UserPromptSubmit, SessionReady, Notification, PostToolUse];

    /// <summary>
    /// Claude Code binding set. Adds the permission-park pair on top of the turn-boundary pair: a
    /// <c>Notification</c> scoped by matcher to <c>permission_prompt</c> (so <c>auth_success</c> /
    /// <c>idle_prompt</c> never spuriously park), and <c>PostToolUse</c> to un-park after approval.
    /// </summary>
    public static readonly IReadOnlyList<TerminalHookBinding> ClaudeBindings =
    [
        new(Stop),
        new(UserPromptSubmit),
        new(Notification, Matcher: "permission_prompt"),
        new(PostToolUse),
    ];

    /// <summary>
    /// Codex binding set — turn-boundary pair only. The permission-park pair is Claude-specific
    /// (matcher semantics and the <c>Notification</c>/<c>PostToolUse</c> taxonomy are Claude Code's)
    /// and is intentionally left out until Codex's hook events are verified.
    /// </summary>
    public static readonly IReadOnlyList<TerminalHookBinding> CodexBindings =
    [
        new(Stop),
        new(UserPromptSubmit),
    ];

    /// <summary>
    /// OpenCode plugin-event mapping verified against opencode-ai 1.17.7 docs/source: local
    /// project plugins are auto-loaded from <c>.opencode/plugins</c>, <c>session.idle</c> is the
    /// turn-yield signal, <c>tui.prompt.append</c> fires when the operator submits new TUI input,
    /// and the permission/tool events cover approval park/resume without Claude/Codex matchers.
    /// </summary>
    public const string OpenCodeBindingEvent = "event";
    public const string OpenCodeBindingTypedHook = "typed_hook";

    public static readonly IReadOnlyList<OpenCodeHookBinding> OpenCodeBindings =
    [
        new("session.created", SessionReady, OpenCodeBindingEvent),
        new("session.idle", Stop, OpenCodeBindingEvent),
        new("tui.prompt.append", UserPromptSubmit, OpenCodeBindingEvent),
        new("permission.asked", Notification, OpenCodeBindingEvent),
        new("permission.replied", PostToolUse, OpenCodeBindingEvent),
        new("tool.execute.after", PostToolUse, OpenCodeBindingTypedHook),
    ];
}

/// <summary>
/// One injected hook binding: the runtime event plus an optional vendor matcher that scopes which
/// instances of the event fire the callback (e.g. Claude's <c>notification_type</c> matcher).
/// </summary>
public sealed record TerminalHookBinding(string Event, string? Matcher = null);

/// <summary>
/// Maps one OpenCode plugin event name to the existing Throne terminal-hook event wire name.
/// </summary>
public sealed record OpenCodeHookBinding(string OpenCodeHook, string ThroneEvent, string BindingType);
