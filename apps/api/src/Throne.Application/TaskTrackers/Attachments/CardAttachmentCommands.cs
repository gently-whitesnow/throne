namespace Throne.Application.TaskTrackers.Attachments;

/// <summary>
/// Attach a task-tracker card to an intent as read-only context (ADR-0052). Idempotent by the
/// <c>(Tracker, BoardId, CardId)</c> coordinate within the intent. Only the HTTP module dispatches this.
/// </summary>
public sealed record AttachCardCommand(string IntentId, string Tracker, string BoardId, string CardId);

/// <summary>Detach a card from an intent. Idempotent — a missing/foreign attachment is a silent no-op.</summary>
public sealed record DetachCardCommand(string IntentId, string AttachmentId);

/// <summary>
/// Manual «Обновить» re-pull of an attachment's snapshot (ADR-0052). Online-only and degrade-without-error:
/// an unreachable tracker keeps the previous snapshot, a vanished card marks it <c>gone</c>.
/// </summary>
public sealed record RefreshCardAttachmentCommand(string IntentId, string AttachmentId);
