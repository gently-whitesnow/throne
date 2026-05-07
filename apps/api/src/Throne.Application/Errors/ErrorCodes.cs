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
    public const string InstructionVersionConflict = "instruction.version_conflict";
    public const string InstructionTextMatchNotFound = "instruction.text.match_not_found";
    public const string InstructionTextMatchAmbiguous = "instruction.text.match_ambiguous";
    public const string TagNotFound = "tag.not_found";
    public const string TagVersionConflict = "tag.version_conflict";
    public const string TagNameTaken = "tag.name_taken";
    public const string TagNameInvalid = "tag.name_invalid";
    public const string TagInUse = "tag.in_use";
    public const string DreamRunNotFound = "dream.run.not_found";
    public const string DreamProposalNotFound = "dream.proposal.not_found";
    public const string DreamProposalAlreadyDecided = "dream.proposal.already_decided";
    public const string DreamRunAlreadyClosed = "dream.run.already_closed";
    public const string DreamProposalNeedsRebase = "dream.proposal.needs_rebase";
    public const string DreamProposalCapReached = "dream.proposal.cap_reached";
    public const string DreamProposalEvidenceUnknown = "dream.proposal.evidence_unknown";
    public const string DreamRunHasProposals = "dream.run.has_proposals";
    public const string ChatUploadTooLarge = "chat_upload.too_large";
    public const string ChatUploadManifestInvalid = "chat_upload.manifest_invalid";
    public const string ChatUploadArchiveInvalid = "chat_upload.archive_invalid";
    public const string ChatUploadSchemaUnsupported = "chat_upload.schema_unsupported";
    public const string ChatUploadNotFound = "chat_upload.not_found";
}
