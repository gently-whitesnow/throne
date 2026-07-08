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
    BoardCardBrowserService cardBrowser) : TaskTrackersControllerBase
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
        return new OkObjectResult(TaskTrackerCardDtoMapper.ToResponse(cards));
    }

    public override async Task<ActionResult<TaskTrackerCardDto>> GetBoardCard(
        string tracker, string board, string card)
    {
        var result = await cardBrowser.GetBoardCardAsync(tracker, board, card, HttpContext.RequestAborted);
        return new OkObjectResult(TaskTrackerCardDtoMapper.ToDto(result));
    }

    private static TaskTrackerProviderDto ToDto(ITaskTrackerProvider provider) => new()
    {
        Tracker = provider.TrackerKey,
        Display_name = provider.DisplayName,
    };
}
