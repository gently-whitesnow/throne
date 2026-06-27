using Microsoft.AspNetCore.Mvc;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Settings → «Таск-трекеры»: the board half. Reads the live space/board topology through the saved
/// connection and folds in the selection, and persists a replacement selection (with per-board
/// grouping context). Both require a configured connection (409 otherwise) and surface an upstream
/// failure as 502 via the provider.
/// </summary>
public sealed class TaskTrackerBoardsEndpoint(
    ITaskTrackerProviderRegistry registry,
    ITaskTrackerConnectionStore store)
{
    public async Task<ActionResult<TaskTrackerBoardsDto>> GetAsync(string tracker, CancellationToken ct)
    {
        var (provider, stored) = await ResolveAsync(tracker, ct);
        var topology = await provider.ListBoardsAsync(
            new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token), ct);
        return new OkObjectResult(
            TaskTrackerSettingsDtoMapper.Boards(tracker, topology, stored.Selection));
    }

    public async Task<ActionResult<TaskTrackerBoardsDto>> SetAsync(
        string tracker,
        UpdateTaskTrackerBoardsRequest body,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var (provider, stored) = await ResolveAsync(tracker, ct);

        var selection = TaskTrackerSettingsDtoMapper.Selection(body);
        await store.SaveSelectionAsync(tracker, selection, ct);

        var topology = await provider.ListBoardsAsync(
            new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token), ct);
        return new OkObjectResult(
            TaskTrackerSettingsDtoMapper.Boards(tracker, topology, selection));
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
