using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class GetIntentLinksSummaryEndpoint
{
    public static async Task<ActionResult<IntentLinksSummaryDto>> RunAsync(IEnumerable<string> ids, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var handler = http.RequestServices.GetRequiredService<GetIntentLinksSummaryHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var summaries = await handler.HandleAsync(
                new GetIntentLinksSummaryQuery(idList),
                http.RequestAborted);

            var tagMap = await helpers.BuildTagMapAsync(CollectTagIds(summaries), http.RequestAborted);
            return new OkObjectResult(new IntentLinksSummaryDto { Items = BuildEntries(summaries, tagMap) });
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            return new UnprocessableEntityObjectResult(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity, "Validation failed", ex));
        }
    }

    private static System.Collections.ObjectModel.Collection<IntentLinksSummaryEntryDto> BuildEntries(
        IReadOnlyList<IntentLinksSummary> summaries,
        IReadOnlyDictionary<string, Tag> tagMap)
    {
        var entries = new System.Collections.ObjectModel.Collection<IntentLinksSummaryEntryDto>();
        foreach (var summary in summaries)
        {
            entries.Add(BuildEntry(summary, tagMap));
        }
        return entries;
    }

    private static IntentLinksSummaryEntryDto BuildEntry(
        IntentLinksSummary summary,
        IReadOnlyDictionary<string, Tag> tagMap) => new()
        {
            Intent_id = summary.IntentId,
            Blocked_by = ToPeerCollection(summary.BlockedBy, tagMap),
            Derived_from = ToPeerCollection(summary.DerivedFrom, tagMap),
            Source_of = ToPeerCollection(summary.SourceOf, tagMap),
            Relates = ToPeerCollection(summary.Relates, tagMap),
        };

    private static IEnumerable<TagId> CollectTagIds(IReadOnlyList<IntentLinksSummary> summaries)
    {
        foreach (var summary in summaries)
        {
            foreach (var peer in summary.BlockedBy.Concat(summary.DerivedFrom).Concat(summary.SourceOf).Concat(summary.Relates))
            {
                foreach (var tagId in peer.TagIds)
                {
                    yield return tagId;
                }
            }
        }
    }

    private static System.Collections.ObjectModel.Collection<IntentLinkPeerDto> ToPeerCollection(
        IReadOnlyList<Intent> peers,
        IReadOnlyDictionary<string, Tag> tagMap)
    {
        var result = new System.Collections.ObjectModel.Collection<IntentLinkPeerDto>();
        foreach (var peer in peers)
        {
            result.Add(IntentDtoMapper.ToPeerDto(peer, tagMap));
        }
        return result;
    }
}
