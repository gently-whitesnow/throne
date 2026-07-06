using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.TaskTrackers.Attachments;

/// <summary>
/// Attach / detach / refresh / list of task-tracker cards as read-only intent context (ADR-0052).
/// Attach requires a live pull; refresh is online-only but degrades without error; detach is idempotent.
/// Writes go through <see cref="IUnitOfWork.ExecuteAsync"/> (the EF store demands an ambient session). No
/// domain events are emitted — realtime attach/detach is deferred (ADR-0052).
/// </summary>
public sealed class CardAttachmentService(
    CardAttachmentResolver resolver,
    IIntentCardAttachmentStore store,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <summary>
    /// Attach a card: resolve intent (404), build the coordinate (422), resolve a connected provider
    /// (422 unsupported / 409 not-connected), pull the card (502 on throw, 404 when gone), then upsert —
    /// idempotent by coordinate (existing → refresh snapshot, absent → create).
    /// </summary>
    public async Task<IntentCardAttachment> AttachAsync(AttachCardCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var intentId = await resolver.EnsureIntentExistsAsync(command.IntentId, ct);
        var requestedCoordinate = BuildCoordinate(command);
        var connection = await resolver.ResolveConnectionAsync(requestedCoordinate.Tracker, ct);

        var card = await PullOrThrowAsync(connection, requestedCoordinate.CardId, ct)
            ?? throw CardAttachmentFailures.CardNotFound(requestedCoordinate.Tracker, requestedCoordinate.CardId);

        var now = clock.GetUtcNow();
        var coordinate = CanonicalizeCoordinate(requestedCoordinate, card);
        var snapshot = CardSnapshotFactory.From(card, now);
        var existing = await store.GetByCoordinateAsync(intentId, coordinate, ct);
        var attachment = ApplyOrCreate(existing, intentId, coordinate, snapshot, now);
        return await PersistAsync(attachment, ct);
    }

    /// <summary>
    /// Re-pull an attachment's snapshot. Provider/connection absent or a pull throw → keep the snapshot
    /// and mark <c>unavailable</c>; a vanished card (null) → <c>gone</c>; success → refresh + <c>available</c>.
    /// </summary>
    public async Task<IntentCardAttachment> RefreshAsync(RefreshCardAttachmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attachment = await resolver.LoadAttachmentAsync(command.IntentId, command.AttachmentId, ct);
        var now = clock.GetUtcNow();

        var connection = await resolver.TryResolveConnectionAsync(attachment.Coordinate.Tracker, ct);
        if (connection is null)
        {
            attachment.MarkUnavailable(CardAvailabilityNames.Unavailable, now);
            return await PersistAsync(attachment, ct);
        }

        var card = await PullOrDegradeAsync(connection, attachment, now, ct);
        if (card is not null && CardMatchesCoordinate(card, attachment.Coordinate))
        {
            attachment.ApplySnapshot(CardSnapshotFactory.From(card, now), now);
        }
        else if (card is not null)
        {
            attachment.MarkUnavailable(CardAvailabilityNames.Gone, now);
        }
        return await PersistAsync(attachment, ct);
    }

    /// <summary>Detach a card. Missing / foreign attachment is a silent no-op (idempotent).</summary>
    public async Task DetachAsync(DetachCardCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attachment = await resolver.FindAttachmentAsync(command.IntentId, command.AttachmentId, ct);
        if (attachment is null)
        {
            return;
        }
        await unitOfWork.ExecuteAsync(c => store.DeleteAsync(attachment.Id, c), ct);
    }

    /// <summary>List every attachment on an intent (404 when the intent is unknown).</summary>
    public async Task<IReadOnlyList<IntentCardAttachment>> ListAsync(string intentId, CancellationToken ct)
    {
        var resolved = await resolver.EnsureIntentExistsAsync(intentId, ct);
        return await store.ListByIntentAsync(resolved, ct);
    }

    private static CardCoordinate BuildCoordinate(AttachCardCommand command)
    {
        try
        {
            return new CardCoordinate(command.Tracker, command.BoardId, command.CardId);
        }
        catch (ArgumentException ex)
        {
            throw CardAttachmentFailures.InvalidCoordinate(
                command.Tracker, command.BoardId, command.CardId, ex.Message);
        }
    }

    private static CardCoordinate CanonicalizeCoordinate(CardCoordinate requested, TaskTrackerCard card)
    {
        if (!string.Equals(card.BoardId, requested.BoardId, StringComparison.Ordinal))
        {
            throw CardAttachmentFailures.InvalidCoordinate(
                requested.Tracker,
                requested.BoardId,
                requested.CardId,
                $"Card '{requested.CardId}' belongs to board '{card.BoardId}', not requested board '{requested.BoardId}'.");
        }

        try
        {
            return new CardCoordinate(requested.Tracker, card.BoardId, card.CardId);
        }
        catch (ArgumentException ex)
        {
            throw CardAttachmentFailures.InvalidCoordinate(
                requested.Tracker, card.BoardId, card.CardId, ex.Message);
        }
    }

    private static bool CardMatchesCoordinate(TaskTrackerCard card, CardCoordinate coordinate) =>
        string.Equals(card.BoardId, coordinate.BoardId, StringComparison.Ordinal)
        && string.Equals(card.CardId, coordinate.CardId, StringComparison.Ordinal);

    private static async Task<TaskTrackerCard?> PullOrThrowAsync(
        CardTrackerConnection connection, string cardId, CancellationToken ct)
    {
        try
        {
            return await connection.Provider.GetCardAsync(connection.Connection, cardId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CardAttachmentFailures.TrackerUnavailable(connection.Provider.TrackerKey, ex.Message);
        }
    }

    private static async Task<TaskTrackerCard?> PullOrDegradeAsync(
        CardTrackerConnection connection, IntentCardAttachment attachment, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var card = await connection.Provider.GetCardAsync(connection.Connection, attachment.Coordinate.CardId, ct);
            if (card is null)
            {
                attachment.MarkUnavailable(CardAvailabilityNames.Gone, now);
            }
            return card;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            attachment.MarkUnavailable(CardAvailabilityNames.Unavailable, now);
            return null;
        }
    }

    private static IntentCardAttachment ApplyOrCreate(
        IntentCardAttachment? existing,
        IntentId intentId,
        CardCoordinate coordinate,
        CardSnapshot snapshot,
        DateTimeOffset now)
    {
        if (existing is not null)
        {
            existing.ApplySnapshot(snapshot, now);
            return existing;
        }
        return IntentCardAttachment.Create(CardAttachmentId.New(), intentId, coordinate, snapshot, now);
    }

    private async Task<IntentCardAttachment> PersistAsync(IntentCardAttachment attachment, CancellationToken ct)
    {
        return await unitOfWork.ExecuteAsync(c => store.UpsertAsync(attachment, c), ct);
    }
}
