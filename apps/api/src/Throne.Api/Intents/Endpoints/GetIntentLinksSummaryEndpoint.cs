using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class GetIntentLinksSummaryEndpoint(
    GetIntentLinksSummaryHandler handler,
    IntentsApiHelpers helpers
)
{
    public async Task<ActionResult<IntentLinksSummaryDto>> RunAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var summaries = await handler.HandleAsync(
            new GetIntentLinksSummaryQuery(idList),
            cancellationToken
        );

        var tagMap = await helpers.BuildTagMapAsync(
            IntentLinksSummaryMapper.CollectTagIds(summaries),
            cancellationToken
        );
        return new OkObjectResult(
            new IntentLinksSummaryDto
            {
                Items = IntentLinksSummaryMapper.BuildEntries(summaries, tagMap),
            }
        );
    }
}

/// <summary>
/// Pure helper functions for mapping <see cref="IntentLinksSummary"/> records
/// into their DTO collections. Split out of <see cref="GetIntentLinksSummaryEndpoint"/>
/// so the endpoint class stays under the CA1502 type-level cyclomatic budget;
/// no state, no dependencies — safe as a static class per the helper convention.
/// </summary>
internal static class IntentLinksSummaryMapper
{
    public static System.Collections.ObjectModel.Collection<IntentLinksSummaryEntryDto> BuildEntries(
        IReadOnlyList<IntentLinksSummary> summaries,
        IReadOnlyDictionary<string, Tag> tagMap
    )
    {
        var entries = new System.Collections.ObjectModel.Collection<IntentLinksSummaryEntryDto>();
        foreach (var summary in summaries)
        {
            entries.Add(BuildEntry(summary, tagMap));
        }
        return entries;
    }

    public static IEnumerable<TagId> CollectTagIds(IReadOnlyList<IntentLinksSummary> summaries)
    {
        foreach (var summary in summaries)
        {
            foreach (
                var peer in summary
                    .BlockedBy.Concat(summary.DerivedFrom)
                    .Concat(summary.SourceOf)
                    .Concat(summary.Relates)
            )
            {
                foreach (var tagId in peer.TagIds)
                {
                    yield return tagId;
                }
            }
        }
    }

    private static IntentLinksSummaryEntryDto BuildEntry(
        IntentLinksSummary summary,
        IReadOnlyDictionary<string, Tag> tagMap
    ) =>
        new()
        {
            Intent_id = summary.IntentId,
            Blocked_by = ToPeerCollection(summary.BlockedBy, tagMap),
            Derived_from = ToPeerCollection(summary.DerivedFrom, tagMap),
            Source_of = ToPeerCollection(summary.SourceOf, tagMap),
            Relates = ToPeerCollection(summary.Relates, tagMap),
        };

    private static System.Collections.ObjectModel.Collection<IntentLinkPeerDto> ToPeerCollection(
        IReadOnlyList<Intent> peers,
        IReadOnlyDictionary<string, Tag> tagMap
    )
    {
        var result = new System.Collections.ObjectModel.Collection<IntentLinkPeerDto>();
        foreach (var peer in peers)
        {
            result.Add(IntentDtoMapper.ToPeerDto(peer, tagMap));
        }
        return result;
    }
}
