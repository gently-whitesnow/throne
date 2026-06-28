using Microsoft.AspNetCore.Mvc;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Settings → «Таск-трекеры»: the connection half. Lists every registered provider with its saved
/// connection state, and upserts / removes the per-provider connection. Token validation runs against
/// the provider on upsert; a rejected or unreachable host is reported as state, not an error response,
/// so the card can render it inline.
/// </summary>
public sealed class TaskTrackerConnectionsEndpoint(
    ITaskTrackerProviderRegistry registry,
    ITaskTrackerConnectionStore store,
    TaskTrackerBoardCatalog catalog)
{
    public async Task<ActionResult<TaskTrackerConnectionsDto>> ListAsync(CancellationToken ct)
    {
        var connections = new List<TaskTrackerConnectionDto>();
        foreach (var provider in registry.AllProviders)
        {
            var stored = await store.GetAsync(provider.TrackerKey, ct);
            connections.Add(TaskTrackerSettingsDtoMapper.Connection(
                provider.TrackerKey,
                provider.DisplayName,
                stored is null ? TaskTrackerConnectionState.Not_configured : TaskTrackerConnectionState.Connected,
                stored?.BaseUrl,
                error: null));
        }

        return new OkObjectResult(new TaskTrackerConnectionsDto { Connections = connections });
    }

    public async Task<ActionResult<TaskTrackerConnectionDto>> SetAsync(
        string tracker,
        UpdateTaskTrackerConnectionRequest body,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var provider = ResolveConnectionProvider(tracker);

        var descriptor = new TaskTrackerConnectionDescriptor(body.Base_url, body.Token);
        var probe = await provider.ProbeAsync(descriptor, ct);
        if (probe.Health == TaskTrackerConnectionHealth.Connected)
        {
            await store.SaveConnectionAsync(tracker, body.Base_url, body.Token, ct);
            // Base URL / token may have changed under the same key — drop any boards cached against the
            // old credentials so the next search re-reads against the new connection.
            catalog.Invalidate(tracker);
            return new OkObjectResult(TaskTrackerSettingsDtoMapper.Connection(
                provider.TrackerKey,
                provider.DisplayName,
                TaskTrackerConnectionState.Connected,
                body.Base_url,
                error: null));
        }

        return new OkObjectResult(TaskTrackerSettingsDtoMapper.Connection(
            provider.TrackerKey,
            provider.DisplayName,
            TaskTrackerSettingsDtoMapper.ToState(probe.Health),
            baseUrl: null,
            probe.Error));
    }

    public async Task<IActionResult> DeleteAsync(string tracker, CancellationToken ct)
    {
        // Resolve to close the open wire key (422 on an unknown tracker) before touching persistence.
        ResolveConnectionProvider(tracker);
        await store.DeleteAsync(tracker, ct);
        catalog.Invalidate(tracker);
        return new NoContentResult();
    }

    private ITaskTrackerConnectionProvider ResolveConnectionProvider(string tracker)
    {
        var provider = registry.GetByName(tracker)
            ?? throw TaskTrackerFailures.ProviderUnsupported(
                tracker,
                registry.AllProviders.Select(p => p.TrackerKey));
        return provider as ITaskTrackerConnectionProvider
            ?? throw TaskTrackerFailures.ProviderUnsupported(
                tracker,
                registry.AllProviders.OfType<ITaskTrackerConnectionProvider>().Select(p => p.TrackerKey));
    }
}
