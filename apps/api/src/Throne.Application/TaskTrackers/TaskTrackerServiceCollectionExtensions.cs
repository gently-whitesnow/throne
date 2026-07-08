using Microsoft.Extensions.DependencyInjection;
using Throne.Application.TaskTrackers;

namespace Throne.Application;

/// <summary>
/// Composition for the task-tracker axis (ADR-0045/0046). Kept out of the root
/// <see cref="DependencyInjection"/> body so the provider-neutral core owns its own registration —
/// the registry indexes whatever <see cref="ITaskTrackerProvider"/> adapters are registered (none in
/// this slice; an adapter such as Kaiten plugs in with one <c>AddSingleton&lt;ITaskTrackerProvider,
/// …&gt;()</c> line and is picked up here).
/// </summary>
public static class TaskTrackerServiceCollectionExtensions
{
    public static IServiceCollection AddTaskTrackerAxis(this IServiceCollection services)
    {
        services.AddSingleton<ITaskTrackerProviderRegistry, TaskTrackerProviderRegistry>();
        services.AddSingleton<TaskTrackerBoardCatalog>();
        services.AddSingleton<BoardCardBrowserService>();
        return services;
    }
}
