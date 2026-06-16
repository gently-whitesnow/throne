using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class GetIntentContextsEndpoint(GetIntentContextsHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentContextCountsDto>> RunAsync(CancellationToken cancellationToken)
    {
        var counts = await handler.HandleAsync(cancellationToken);

        var tagIds = counts.Tags
            .Concat(counts.ArchiveTags)
            .Concat(counts.FridgeTags)
            .Select(t => new TagId(t.TagId));
        var tagMap = await helpers.BuildTagMapAsync(tagIds, cancellationToken);

        return new OkObjectResult(new IntentContextCountsDto
        {
            Inbox_review = counts.InboxReview,
            Inbox_help = counts.InboxHelp,
            Fridge = counts.Fridge,
            Archive = counts.Archive,
            Pinned = counts.Pinned,
            Untagged = counts.Untagged,
            Archive_untagged = counts.ArchiveUntagged,
            Fridge_untagged = counts.FridgeUntagged,
            Terminal_running = counts.TerminalRunning,
            Tags = ToTagRows(counts.Tags, tagMap),
            Archive_tags = ToTagRows(counts.ArchiveTags, tagMap),
            Fridge_tags = ToTagRows(counts.FridgeTags, tagMap),
        });
    }

    // Resolve tag id -> display name, drop ids whose tag no longer exists (mirrors the list DTO
    // tag mapping), then order by count desc then name asc so the rail renders without re-sorting.
    private static Collection<IntentContextTagCountDto> ToTagRows(
        IReadOnlyList<IntentTagCount> counts,
        IReadOnlyDictionary<string, Tag> tagMap)
    {
        var rows = new List<IntentContextTagCountDto>(counts.Count);
        foreach (var count in counts)
        {
            if (!tagMap.TryGetValue(count.TagId, out var tag))
            {
                continue;
            }
            rows.Add(new IntentContextTagCountDto { Tag = tag.Name, Count = count.Count });
        }
        rows.Sort(static (a, b) =>
            a.Count != b.Count ? b.Count - a.Count : string.CompareOrdinal(a.Tag, b.Tag));
        return new Collection<IntentContextTagCountDto>(rows);
    }
}
