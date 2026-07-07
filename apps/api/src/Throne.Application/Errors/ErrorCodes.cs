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
    public const string TagNotFound = "tag.not_found";
    public const string TagVersionConflict = "tag.version_conflict";
    public const string TagNameTaken = "tag.name_taken";
    public const string TagNameInvalid = "tag.name_invalid";
    public const string TagInUse = "tag.in_use";
    public const string PromptPartPatchNotFound = "prompt_part_patch.not_found";
    public const string PromptPartPatchAlreadyDecided = "prompt_part_patch.already_decided";
    public const string PromptPartPatchNeedsRebase = "prompt_part_patch.needs_rebase";
    public const string DreamSessionNotFound = "dream_session.not_found";
    public const string RepositoryBindingNotFound = "repository_binding.not_found";
    public const string RepositoryBindingAlreadyExists = "repository_binding.already_exists";
    public const string RepositoryProviderUnsupported = "repository.provider_unsupported";
    public const string RepositoryProviderNotAuthenticated = "repository.provider_not_authenticated";
    public const string RepositoryNotReady = "repository_binding.not_ready";
    public const string RepositoryPullRequestNotAttached = "repository_binding.pull_request_not_attached";
    public const string RepositoryPullRequestAlreadyAttached = "repository_binding.pull_request_already_attached";
    public const string RepositoryUpstreamGone = "repository.upstream_gone";
    public const string RepositoryReviewAnchorInvalid = "repository_binding.review.anchor_invalid";
    public const string RepositoryBlobNotFound = "repository.blob_not_found";
    public const string RepositoryPullRequestMergeRejected = "repository_binding.pull_request.merge_rejected";
    public const string RepositoryWorkspaceRemovalFailed = "repository_binding.workspace_removal_failed";
    public const string RepositoryBranchSyncFailed = "repository_binding.branch_sync_failed";
    public const string PullRequestArtifactNotFound = "pull_request_artifact.not_found";
    public const string RepositoryCoordinateInvalid = "repository.coordinate_invalid";
    public const string CapabilityNotFound = "capability.not_found";
    public const string CapabilityDisabled = "capability.disabled";
    public const string CapabilityProviderNotFound = "capability.provider_not_found";
    // Single 422 code for all IDE-provider resolution issues; concrete reason
    // (not_selected_and_none_detected / selected_not_detected / ambiguous) is carried
    // in the problem extensions to stay within the ErrorCodes member budget.
    public const string IdeProviderUnavailable = "ide.provider_unavailable";
    public const string GitLabHostInvalid = "settings.gitlab_host.invalid";
    public const string TaskTrackerProviderUnsupported = "task_tracker.provider_unsupported";
    public const string TaskTrackerConnectionMissing = "task_tracker.connection_missing";
    public const string TaskTrackerConnectionRejected = "task_tracker.connection_rejected";
    public const string TaskTrackerConnectionBlocked = "task_tracker.connection_blocked";
    public const string TaskTrackerUpstreamUnavailable = "task_tracker.upstream_unavailable";
    public const string CardAttachmentIntentNotFound = "card_attachment.intent_not_found";
    public const string CardAttachmentNotFound = "card_attachment.not_found";
    public const string CardAttachmentInvalidCoordinate = "card_attachment.invalid_coordinate";
    public const string CardAttachmentTrackerUnsupported = "card_attachment.tracker_unsupported";
    public const string CardAttachmentTrackerNotConnected = "card_attachment.tracker_not_connected";
    public const string CardAttachmentTrackerUnavailable = "card_attachment.tracker_unavailable";
    public const string CardAttachmentCardNotFound = "card_attachment.card_not_found";
    public const string PromptPartNotFound = "prompt_part.not_found";
    public const string PromptPartAlreadyExists = "prompt_part.already_exists";
    public const string PromptPartVersionConflict = "prompt_part.version_conflict";
    public const string PromptPartHasRoles = "prompt_part.has_roles";

    // Single 422 code for both no-match and ambiguous-match; the reason is carried in the
    // problem extensions (kept as one constant to stay within the ErrorCodes member budget).
    public const string PromptPartTextMatchFailed = "prompt_part.text.match_failed";
}
