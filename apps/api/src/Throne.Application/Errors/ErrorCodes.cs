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
}
