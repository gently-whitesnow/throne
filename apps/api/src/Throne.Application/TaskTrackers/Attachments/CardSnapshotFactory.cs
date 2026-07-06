using Throne.Domain.TaskTrackers;

namespace Throne.Application.TaskTrackers.Attachments;

/// <summary>
/// Projects the provider-neutral <see cref="TaskTrackerCard"/> onto the domain
/// <see cref="CardSnapshot"/> stored on an attachment. <paramref name="fetchedAt"/> stamps the pull time
/// (the snapshot is captured «now»); the provider revision becomes the opaque <c>CardVersion</c>.
/// </summary>
internal static class CardSnapshotFactory
{
    public static CardSnapshot From(TaskTrackerCard card, DateTimeOffset fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new CardSnapshot(
            Title: card.Title,
            Description: card.Description,
            ColumnTitle: card.ColumnTitle,
            Archived: card.Archived,
            CardVersion: card.RevisionTag,
            FetchedAt: fetchedAt);
    }
}
