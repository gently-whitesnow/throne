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
}
