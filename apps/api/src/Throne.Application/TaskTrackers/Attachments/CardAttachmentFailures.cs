using Throne.Application.Errors;

namespace Throne.Application.TaskTrackers.Attachments;

/// <summary>
/// Centralised <see cref="ApiException"/> factory for <see cref="CardAttachmentService"/>. Keeps the
/// service body focused on workflow and free of error-construction boilerplate.
/// </summary>
internal static class CardAttachmentFailures
{
    public static ApiException IntentNotFound(string intentId) =>
        new(
            ErrorCodes.CardAttachmentIntentNotFound,
            $"Intent '{intentId}' not found.",
            new Dictionary<string, object?> { ["intent_id"] = intentId });

    public static ApiException AttachmentNotFound(string intentId, string attachmentId) =>
        new(
            ErrorCodes.CardAttachmentNotFound,
            $"Card attachment '{attachmentId}' not found for intent '{intentId}'.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["attachment_id"] = attachmentId,
            });

    public static ApiException InvalidCoordinate(string tracker, string boardId, string cardId, string detail) =>
        new(
            ErrorCodes.CardAttachmentInvalidCoordinate,
            detail,
            new Dictionary<string, object?>
            {
                ["tracker"] = tracker,
                ["board_id"] = boardId,
                ["card_id"] = cardId,
            });

    public static ApiException TrackerUnsupported(string tracker) =>
        new(
            ErrorCodes.CardAttachmentTrackerUnsupported,
            $"Task tracker '{tracker}' is not supported on this Throne build.",
            new Dictionary<string, object?> { ["tracker"] = tracker });

    public static ApiException TrackerNotConnected(string tracker) =>
        new(
            ErrorCodes.CardAttachmentTrackerNotConnected,
            $"No connection is configured for task tracker '{tracker}'.",
            new Dictionary<string, object?> { ["tracker"] = tracker });

    public static ApiException TrackerUnavailable(string tracker, string detail) =>
        new(
            ErrorCodes.CardAttachmentTrackerUnavailable,
            $"The '{tracker}' task-tracker API could not be reached: {detail}",
            new Dictionary<string, object?> { ["tracker"] = tracker });

    public static ApiException CardNotFound(string tracker, string cardId) =>
        new(
            ErrorCodes.CardAttachmentCardNotFound,
            $"Card '{cardId}' is not readable on task tracker '{tracker}' (vanished or forbidden).",
            new Dictionary<string, object?>
            {
                ["tracker"] = tracker,
                ["card_id"] = cardId,
            });
}
