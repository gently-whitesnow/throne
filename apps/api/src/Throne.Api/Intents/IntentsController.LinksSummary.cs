using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// Companion endpoint to <see cref="IntentsControllerBase.ListIntents"/>. Keeps
/// the list DTO graph-free and serves the per-intent link aggregates that the
/// board renders as badges and hover-overlay (ADR-0019 follow-up).
/// </summary>
public sealed partial class IntentsController
{
    public override async Task<ActionResult<IntentLinksSummaryDto>> GetIntentLinksSummary(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        try
        {
            var summaries = await linksSummaryHandler.HandleAsync(
                new GetIntentLinksSummaryQuery(idList),
                HttpContext.RequestAborted);

            var tagMap = await BuildTagMapAsync(CollectTagIds(summaries), HttpContext.RequestAborted);

            var entries = new System.Collections.ObjectModel.Collection<IntentLinksSummaryEntryDto>();
            foreach (var summary in summaries)
            {
                entries.Add(BuildEntry(summary, tagMap));
            }
            return Ok(new IntentLinksSummaryDto { Items = entries });
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            return UnprocessableEntity(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity, "Validation failed", ex));
        }
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
