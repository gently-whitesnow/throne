namespace Throne.Application.Errors;

public static class ErrorCodes
{
    public const string IntentNotFound = "intent.not_found";
    public const string IntentVersionConflict = "intent.version_conflict";
    public const string IntentTextMatchNotFound = "intent.text.match_not_found";
    public const string IntentTextMatchAmbiguous = "intent.text.match_ambiguous";
    public const string IntentTextLineOutOfRange = "intent.text.line_out_of_range";
    public const string ValidationFailed = "validation.failed";
    public const string IntentAttachmentTooLarge = "intent.attachment.too_large";
    public const string IntentAttachmentLimitExceeded = "intent.attachment.limit_exceeded";
    public const string IntentAttachmentNotFound = "intent.attachment.not_found";
    public const string InstructionNotFound = "instruction.not_found";
    public const string InstructionAlreadyExists = "instruction.already_exists";
    public const string InstructionVersionConflict = "instruction.version_conflict";
    public const string InstructionTextMatchNotFound = "instruction.text.match_not_found";
    public const string InstructionTextMatchAmbiguous = "instruction.text.match_ambiguous";
    public const string TagNotFound = "tag.not_found";
    public const string TagVersionConflict = "tag.version_conflict";
    public const string TagNameTaken = "tag.name_taken";
    public const string TagNameInvalid = "tag.name_invalid";
    public const string TagInUse = "tag.in_use";
    public const string InstructionPatchNotFound = "instruction_patch.not_found";
    public const string InstructionPatchAlreadyDecided = "instruction_patch.already_decided";
    public const string InstructionPatchNeedsRebase = "instruction_patch.needs_rebase";
    public const string DreamSessionNotFound = "dream_session.not_found";
    public const string RepositoryBindingNotFound = "repository_binding.not_found";
    public const string RepositoryBindingAlreadyExists = "repository_binding.already_exists";
    public const string RepositoryProviderUnsupported = "repository.provider_unsupported";
    public const string RepositoryProviderNotAuthenticated = "repository.provider_not_authenticated";
    public const string RepositoryNotReady = "repository_binding.not_ready";
    public const string RepositoryPullRequestNotAttached = "repository_binding.pull_request_not_attached";
    public const string RepositoryUpstreamGone = "repository.upstream_gone";
    public const string CapabilityNotFound = "capability.not_found";
    public const string CapabilityDisabled = "capability.disabled";
    public const string TerminalSessionAlreadyRunning = "terminal.session_already_running";
    public const string TerminalRunPreflightBlocked = "terminal.run_preflight_blocked";
    public const string TerminalSpawnFailed = "terminal.spawn_failed";
    public const string TerminalCloneWaitTimeout = "terminal.clone_wait_timeout";
    public const string TerminalModeInvalid = "terminal.mode_invalid";
}
