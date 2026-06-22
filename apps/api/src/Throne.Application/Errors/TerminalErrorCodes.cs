namespace Throne.Application.Errors;

public static class TerminalErrorCodes
{
    public const string SessionAlreadyRunning = "terminal.session_already_running";
    public const string SessionNotLive = "terminal.session_not_live";
    public const string SessionSkillUnknown = "terminal.session_skill.unknown";
    public const string SessionSkillNotMaterializable = "terminal.session_skill.not_materializable";
    public const string SessionSkillVendorUnsupported = "terminal.session_skill.vendor_unsupported";
    public const string RunPreflightBlocked = "terminal.run_preflight_blocked";
    public const string SpawnFailed = "terminal.spawn_failed";
    public const string TuiReadinessTimeout = "terminal.tui_readiness_timeout";
    public const string InitialPromptSubmitFailed = "terminal.initial_prompt_submit_failed";
    public const string CloneWaitTimeout = "terminal.clone_wait_timeout";
    public const string ModeInvalid = "terminal.mode_invalid";
    public const string ArgsInvalid = "terminal.args_invalid";
}
