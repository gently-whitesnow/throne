using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Events;
using Throne.Application.TaskTrackers.Sync;

namespace Throne.Application;

/// <summary>
/// Registers the task-tracker two-way sync services (ADR-0008): the per-tick poll orchestration plus
/// the write-through domain-event handler. Kept as one extension so the composition root references a
/// single entry point instead of fanning out over every sync collaborator. The hosted timer that calls
/// <c>TaskTrackerSyncTickWorkflow.RunAsync</c> lives in Infrastructure, like <c>PullRequestSyncService</c>.
/// </summary>
public static class TaskTrackerSyncServiceCollectionExtensions
{
    public static IServiceCollection AddTaskTrackerSync(this IServiceCollection services)
    {
        services.AddSingleton<TaskTrackerSyncBackoff>();
        services.AddSingleton<TaskTrackerMirrorService>();
        services.AddSingleton<TaskTrackerChildLinkReconciler>();
        services.AddSingleton<TaskTrackerBoardSyncWorkflow>();
        services.AddSingleton<TaskTrackerSyncTickWorkflow>();
        services.AddSingleton<TaskTrackerForcePullWorkflow>();
        services.AddSingleton<TaskTrackerBoardForcePullWorkflow>();
        services.AddSingleton<IDomainEventHandler, TaskTrackerWriteThroughHandler>();
        return services;
    }
}
