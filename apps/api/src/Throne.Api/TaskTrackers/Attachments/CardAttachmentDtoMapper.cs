using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.CardAttachments.Contracts.Generated;
using Throne.Domain.TaskTrackers;

namespace Throne.Api.TaskTrackers.Attachments;

/// <summary>
/// Wire-format translation for the card-attachment slice: domain <see cref="IntentCardAttachment"/> →
/// generated <see cref="CardAttachmentDto"/>. Enriches the DTO with a per-request derived
/// <c>web_url</c> composed by the tracker provider from the saved connection (ADR-0052:
/// non-authoritative, never persisted with the snapshot).
/// </summary>
public sealed class CardAttachmentDtoMapper(
    ITaskTrackerProviderRegistry providers,
    ITaskTrackerConnectionStore connections)
{
    public async Task<CardAttachmentDto> ToDtoAsync(IntentCardAttachment attachment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var webUrl = await ResolveWebUrlAsync(attachment.Coordinate, ct);
        return Build(attachment, webUrl);
    }

    public async Task<IReadOnlyList<CardAttachmentDto>> ToDtoListAsync(
        IReadOnlyList<IntentCardAttachment> attachments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        // One connection read per distinct tracker keeps the list endpoint O(providers), not O(attachments).
        var trackers = attachments.Select(a => a.Coordinate.Tracker).Distinct(StringComparer.Ordinal);
        var connectionByTracker = new Dictionary<string, TaskTrackerStoredConnection?>(StringComparer.Ordinal);
        foreach (var tracker in trackers)
        {
            connectionByTracker[tracker] = await connections.GetAsync(tracker, ct);
        }

        var result = new List<CardAttachmentDto>(attachments.Count);
        foreach (var attachment in attachments)
        {
            connectionByTracker.TryGetValue(attachment.Coordinate.Tracker, out var stored);
            var webUrl = BuildWebUrl(attachment.Coordinate, stored);
            result.Add(Build(attachment, webUrl));
        }
        return result;
    }

    public static CardAvailability ToWireAvailability(string value) => value switch
    {
        CardAvailabilityNames.Available => CardAvailability.Available,
        CardAvailabilityNames.Unavailable => CardAvailability.Unavailable,
        CardAvailabilityNames.Gone => CardAvailability.Gone,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown card availability."),
    };

    private async Task<string?> ResolveWebUrlAsync(CardCoordinate coordinate, CancellationToken ct)
    {
        var stored = await connections.GetAsync(coordinate.Tracker, ct);
        return BuildWebUrl(coordinate, stored);
    }

    private string? BuildWebUrl(CardCoordinate coordinate, TaskTrackerStoredConnection? stored)
    {
        if (stored is null)
        {
            return null;
        }
        if (providers.GetByName(coordinate.Tracker) is not ITaskTrackerConnectionProvider provider)
        {
            return null;
        }
        return provider.BuildCardWebUrl(
            new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token),
            coordinate.CardId);
    }

    private static CardAttachmentDto Build(IntentCardAttachment attachment, string? webUrl)
    {
        var snapshot = attachment.State.Snapshot;
        return new CardAttachmentDto
        {
            Id = attachment.Id.Value,
            Intent_id = attachment.IntentId.Value,
            Tracker = attachment.Coordinate.Tracker,
            Board_id = attachment.Coordinate.BoardId,
            Card_id = attachment.Coordinate.CardId,
            Text = snapshot.Text,
            Column_title = snapshot.ColumnTitle,
            Archived = snapshot.Archived,
            Card_version = snapshot.CardVersion,
            Availability = ToWireAvailability(attachment.State.Availability),
            Web_url = webUrl,
            Fetched_at = snapshot.FetchedAt,
            Created_at = attachment.CreatedAt,
            Updated_at = attachment.State.UpdatedAt,
        };
    }
}
