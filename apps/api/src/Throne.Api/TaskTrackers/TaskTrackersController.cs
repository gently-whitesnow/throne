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
public sealed class TaskTrackersController(ITaskTrackerProviderRegistry registry) : TaskTrackersControllerBase
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

    private static TaskTrackerProviderDto ToDto(ITaskTrackerProvider provider) => new()
    {
        Tracker = provider.TrackerKey,
        Display_name = provider.DisplayName,
    };
}
