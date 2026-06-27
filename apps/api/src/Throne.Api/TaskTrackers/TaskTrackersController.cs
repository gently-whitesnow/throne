using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.TaskTrackers;
using Throne.TaskTrackers.Contracts.Generated;

namespace Throne.Api.TaskTrackers;

/// <summary>
/// Provider-neutral catalog surface for the task-tracker axis (ADR-0045/0046). Both catalog reads
/// resolve straight off <see cref="ITaskTrackerProviderRegistry"/>; resolving an unknown key is the
/// first server boundary that closes the open wire key (422 provider-unsupported). The force-pull
/// action drives the sync axis's open-time / «Обновить» refresh.
/// </summary>
public sealed class TaskTrackersController(
    ITaskTrackerProviderRegistry registry,
    TaskTrackerForcePullWorkflow forcePull) : TaskTrackersControllerBase
{
    public override async Task<ActionResult<TaskTrackerCardSyncDto>> ForceRefreshIntentTaskTrackerCard(string id)
    {
        var result = await forcePull.ForcePullAsync(id, HttpContext.RequestAborted);
        return new OkObjectResult(new TaskTrackerCardSyncDto
        {
            Status = MapStatus(result.Status),
            Tracker = result.Tracker,
            Card_id = result.CardId,
            State = result.State == CardSyncLinkState.Stub
                ? TaskTrackerCardSyncDtoState.Stub
                : TaskTrackerCardSyncDtoState.Linked,
        });
    }

    private static TaskTrackerCardSyncDtoStatus MapStatus(ForcePullStatus status) => status switch
    {
        ForcePullStatus.Refreshed => TaskTrackerCardSyncDtoStatus.Refreshed,
        ForcePullStatus.Stubbed => TaskTrackerCardSyncDtoStatus.Stubbed,
        ForcePullStatus.Unavailable => TaskTrackerCardSyncDtoStatus.Unavailable,
        _ => TaskTrackerCardSyncDtoStatus.Not_linked,
    };

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

    private static TaskTrackerProviderDto ToDto(ITaskTrackerProvider provider) => new()
    {
        Tracker = provider.TrackerKey,
        Display_name = provider.DisplayName,
    };
}
