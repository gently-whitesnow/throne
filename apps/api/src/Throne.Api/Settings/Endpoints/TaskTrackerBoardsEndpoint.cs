using Microsoft.AspNetCore.Mvc;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Settings → «Таск-трекеры»: the board half. Search the board catalog by name (cache-backed, one cheap
/// upstream read), read the saved selection (local-only, for the chips), and replace the selection with
/// its per-board grouping context. All three require a configured connection (409 otherwise); a search
/// that has to (re)read the topology surfaces an upstream failure as 502 via the provider.
/// </summary>
public sealed class TaskTrackerBoardsEndpoint(
    ITaskTrackerProviderRegistry registry,
    ITaskTrackerConnectionStore store,
    TaskTrackerBoardCatalog catalog)
{
    public async Task<ActionResult<TaskTrackerBoardSearchDto>> SearchAsync(
        string tracker,
        string? query,
        int skip,
        int take,
        bool refresh,
        CancellationToken ct)
    {
        var (provider, stored) = await ResolveAsync(tracker, ct);
        var matches = await catalog.SearchAsync(
            tracker,
            provider,
            new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token),
            query,
            skip,
            take,
            refresh,
            ct);
        return new OkObjectResult(TaskTrackerSettingsDtoMapper.SearchResult(tracker, matches));
    }

    public async Task<ActionResult<TaskTrackerBoardSelectionDto>> GetSelectionAsync(
        string tracker,
        CancellationToken ct)
    {
        var (_, stored) = await ResolveAsync(tracker, ct);
        return new OkObjectResult(TaskTrackerSettingsDtoMapper.SelectionView(tracker, stored.Selection));
    }

    public async Task<ActionResult<TaskTrackerBoardSelectionDto>> SetAsync(
        string tracker,
        UpdateTaskTrackerBoardsRequest body,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await ResolveAsync(tracker, ct);

        var selection = TaskTrackerSettingsDtoMapper.Selection(body);
        await store.SaveSelectionAsync(tracker, selection, ct);

        return new OkObjectResult(TaskTrackerSettingsDtoMapper.SelectionView(tracker, selection));
    }

    private async Task<(ITaskTrackerConnectionProvider Provider, TaskTrackerStoredConnection Stored)> ResolveAsync(
        string tracker,
        CancellationToken ct)
    {
        var provider = registry.GetByName(tracker) as ITaskTrackerConnectionProvider
            ?? throw TaskTrackerFailures.ProviderUnsupported(
                tracker,
                registry.AllProviders.OfType<ITaskTrackerConnectionProvider>().Select(p => p.TrackerKey));
        var stored = await store.GetAsync(tracker, ct)
            ?? throw TaskTrackerFailures.ConnectionMissing(tracker);
        return (provider, stored);
    }
}
