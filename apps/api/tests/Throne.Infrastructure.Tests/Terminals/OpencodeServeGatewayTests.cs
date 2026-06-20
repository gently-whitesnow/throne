using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeServeGatewayTests
{
    private static readonly RunPreflightOptions Options = new()
    {
        OpencodeServeHostname = "127.0.0.1",
        OpencodeServePort = 4096,
        OpencodeServeReadinessTimeoutMilliseconds = 500,
        TuiReadinessPollIntervalMilliseconds = 20,
    };

    [Fact(DisplayName = "Здоровый serve — fast path: не спавнит tmux-сессию")]
    public async Task Healthy_serve_does_not_spawn()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        var gateway = NewGateway(tmux, alwaysHealthy: true);

        var endpoint = await gateway.EnsureRunningAsync(CancellationToken.None);

        endpoint.Should().Be(new Uri("http://127.0.0.1:4096/"));
        await tmux.DidNotReceive().SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
        await tmux.DidNotReceive().HasSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Нет сессии и нездоров — спавнит serve под зарезервированным id и ждёт health")]
    public async Task Spawns_serve_when_missing_then_waits_health()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.HasSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        TmuxSpawnRequest? spawned = null;
        tmux.SpawnAsync(Arg.Do<TmuxSpawnRequest>(r => spawned = r), Arg.Any<CancellationToken>())
            .Returns(new TmuxSpawnResult("throne-opencode-serve", IsAlive: true, Detail: null));
        // Unhealthy until the spawn happens, healthy afterwards.
        var handler = new HealthHandler(() => spawned is not null);
        var gateway = NewGateway(tmux, handler);

        var endpoint = await gateway.EnsureRunningAsync(CancellationToken.None);

        endpoint.Should().Be(new Uri("http://127.0.0.1:4096/"));
        spawned!.IntentId.Should().Be(TmuxSessionName.OpencodeServeReservedId);
        spawned.Command.Should().Be(TerminalAgentCatalog.VendorOpencode);
        spawned.Arguments.Should().Equal("serve", "--hostname", "127.0.0.1", "--port", "4096");
        await tmux.DidNotReceive().KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Сессия жива, но нездорова — убивает залипший serve и переспавнивает")]
    public async Task Kills_stale_session_before_respawn()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.HasSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var killed = false;
        tmux.KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => { killed = true; return true; });
        tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TmuxSpawnResult("throne-opencode-serve", IsAlive: true, Detail: null));
        var handler = new HealthHandler(() => killed);
        var gateway = NewGateway(tmux, handler);

        await gateway.EnsureRunningAsync(CancellationToken.None);

        await tmux.Received(1).KillSessionAsync(
            TmuxSessionName.OpencodeServeReservedId, Arg.Any<CancellationToken>());
    }

    private static OpencodeServeGateway NewGateway(
        ITmuxSessionManager tmux, bool alwaysHealthy) =>
        NewGateway(tmux, new HealthHandler(() => alwaysHealthy));

    private static OpencodeServeGateway NewGateway(ITmuxSessionManager tmux, HealthHandler handler)
    {
        var workspaceRoot = Substitute.For<IWorkspaceRootProvider>();
        workspaceRoot.ResolvedRoot.Returns(Path.GetTempPath());
        return new OpencodeServeGateway(
            new Lazy<ITmuxSessionManager>(() => tmux),
            new FixedHttpClientFactory(new HttpClient(handler)),
            Options,
            workspaceRoot,
            TimeProvider.System,
            NullLogger<OpencodeServeGateway>.Instance);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class HealthHandler(Func<bool> healthy) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(
                healthy() ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable));
    }
}
