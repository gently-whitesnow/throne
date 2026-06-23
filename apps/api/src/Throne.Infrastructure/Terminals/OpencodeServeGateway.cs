using System.Globalization;
using Microsoft.Extensions.Logging;
using Throne.Application.Git;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Owns the lifecycle of the single shared <c>opencode serve</c> (ADR-0026 «tmux is the source of
/// truth for liveness»): it runs in a fixed-name tmux session at a fixed configured address, so the
/// agent loops of every OpenCode intent live in one headless server that outlasts any attach front
/// and a Throne restart. The per-intent tmux session only runs <c>opencode attach</c> against it.
///
/// <see cref="EnsureRunningAsync"/> is the only entry point and is the gate every OpenCode spawn
/// passes through before creating its session. A <see cref="SemaphoreSlim"/> serialises the
/// spawn-and-wait so two concurrent Run requests never double-spawn the serve.
/// </summary>
internal sealed class OpencodeServeGateway(
    Lazy<ITmuxSessionManager> tmux,
    IHttpClientFactory httpClientFactory,
    RunPreflightOptions options,
    IWorkspaceRootProvider workspaceRoot,
    TimeProvider clock,
    ILogger<OpencodeServeGateway> logger) : IOpencodeServeGateway, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Lazy<ITmuxSessionManager>: the manager depends on IDomainEventDispatcher → the full handler
    // fan-out → ISessionHookAdapter (this gateway's owner), so eager injection would close that
    // resolution cycle. Same guard as TerminalKillOnIntentCloseHandler.
    private ITmuxSessionManager Tmux => tmux.Value;

    public void Dispose() => _gate.Dispose();

    private Uri BaseUri => new(
        $"http://{options.OpencodeServeHostname}:{options.OpencodeServePort.ToString(CultureInfo.InvariantCulture)}/");

    public async Task<Uri> EnsureRunningAsync(CancellationToken ct)
    {
        var endpoint = BaseUri;
        if (await IsHealthyAsync(endpoint, ct))
        {
            return endpoint;
        }

        await _gate.WaitAsync(ct);
        try
        {
            // Double-checked: another request may have brought the serve up while we queued.
            if (await IsHealthyAsync(endpoint, ct))
            {
                return endpoint;
            }

            var hasSession = await Tmux.HasSessionAsync(TmuxSessionName.OpencodeServeReservedId, ct);
            if (hasSession)
            {
                // Session is alive but health failed → stale/wedged serve on a known address.
                // Kill it so the respawn below binds the port cleanly.
                await Tmux.KillSessionAsync(TmuxSessionName.OpencodeServeReservedId, ct);
            }

            await SpawnServeAsync(ct);
            await WaitForHealthAsync(endpoint, ct);
            TerminalsLog.OpencodeServeReady(logger, endpoint);
            return endpoint;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SpawnServeAsync(CancellationToken ct)
    {
        var port = options.OpencodeServePort.ToString(CultureInfo.InvariantCulture);
        var spawn = await Tmux.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId: TmuxSessionName.OpencodeServeReservedId,
                WorkingDirectory: workspaceRoot.ResolvedRoot,
                Command: TerminalAgentCatalog.VendorOpencode,
                Arguments: ["serve", "--hostname", options.OpencodeServeHostname, "--port", port],
                EnableMouse: false),
            ct);

        if (!spawn.IsAlive)
        {
            throw new InvalidOperationException(
                $"opencode serve tmux session did not start: {spawn.Detail ?? "<no detail>"}");
        }
    }

    private async Task WaitForHealthAsync(Uri endpoint, CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(
            Math.Max(100, options.OpencodeServeReadinessTimeoutMilliseconds));
        var poll = TimeSpan.FromMilliseconds(Math.Max(20, options.TuiReadinessPollIntervalMilliseconds));
        var deadline = clock.GetUtcNow() + timeout;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(endpoint, ct))
            {
                return;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                throw new TimeoutException(
                    $"opencode serve did not become healthy at {endpoint} within {timeout.TotalMilliseconds:0} ms.");
            }

            var remaining = deadline - clock.GetUtcNow();
            await Task.Delay(remaining < poll ? remaining : poll, clock, ct);
        }
    }

    private async Task<bool> IsHealthyAsync(Uri endpoint, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "global/health"));
            OpencodeServerAuth.Apply(request);
            using var response = await httpClientFactory
                .CreateClient(OpencodeTuiClient.HttpClientName)
                .SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
