using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.TaskTrackers;
using Throne.TaskTrackers.Contracts.Generated;

namespace Throne.Api.TaskTrackers;

/// <summary>
/// Provider-neutral catalog surface for the task-tracker axis (ADR-0045/0046). Both catalog reads
/// resolve straight off <see cref="ITaskTrackerProviderRegistry"/>; resolving an unknown key is the
/// first server boundary that closes the open wire key (422 provider-unsupported).
/// </summary>
public sealed class TaskTrackersController(
    ITaskTrackerProviderRegistry registry,
    BoardCardBrowserService cardBrowser,
    TaskTrackerCardDtoMapper cardMapper) : TaskTrackersControllerBase
{
    public override Task<ActionResult<TaskTrackerCatalogResponse>> ListTaskTrackers()
    {
        var response = new TaskTrackerCatalogResponse
        {
            Providers = registry.AllProviders.Select(ToDto).ToList(),
        };
        return Task.FromResult<ActionResult<TaskTrackerCatalogResponse>>(new OkObjectResult(response));
    }

    public override Task<ActionResult<TaskTrackerProviderDto>> GetTaskTracker(string tracker)
    {
        var provider = registry.GetByName(tracker)
            ?? throw TaskTrackerFailures.ProviderUnsupported(
                tracker,
                registry.AllProviders.Select(p => p.TrackerKey));
        return Task.FromResult<ActionResult<TaskTrackerProviderDto>>(new OkObjectResult(ToDto(provider)));
    }

    public override async Task<ActionResult<TaskTrackerBoardCardsResponse>> ListBoardCards(
        string tracker, string board)
    {
        var cards = await cardBrowser.ListBoardCardsAsync(tracker, board, HttpContext.RequestAborted);
        return new OkObjectResult(await cardMapper.ToResponseAsync(tracker, cards, HttpContext.RequestAborted));
    }

    public override async Task<ActionResult<TaskTrackerCardDto>> GetBoardCard(
        string tracker, string board, string card)
    {
        var result = await cardBrowser.GetBoardCardAsync(tracker, board, card, HttpContext.RequestAborted);
        return new OkObjectResult(await cardMapper.ToDtoAsync(tracker, result, HttpContext.RequestAborted));
    }

    public override async Task<ActionResult<TaskTrackerBoardCardsResponse>> SearchBoardCards(
        string tracker, string board, string? query, int? limit)
    {
        var effectiveLimit = ClampLimit(limit);
        var cards = await cardBrowser.SearchBoardCardsAsync(
            tracker, board, query, effectiveLimit, HttpContext.RequestAborted);
        return new OkObjectResult(await cardMapper.ToResponseAsync(tracker, cards, HttpContext.RequestAborted));
    }

    private const int DefaultCardSearchLimit = 10;
    private const int MaxCardSearchLimit = 25;

    private static int ClampLimit(int? limit)
    {
        if (limit is not { } value)
        {
            return DefaultCardSearchLimit;
        }

        return Math.Clamp(value, 1, MaxCardSearchLimit);
    }

    private static TaskTrackerProviderDto ToDto(ITaskTrackerProvider provider) => new()
    {
        Tracker = provider.TrackerKey,
        Display_name = provider.DisplayName,
    };
}
