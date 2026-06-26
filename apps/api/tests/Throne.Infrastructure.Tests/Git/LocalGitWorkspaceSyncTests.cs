using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// «Синхронизировать ветку»: hard-sync рабочего дерева на upstream-tip. Должен быть
/// fork-PR-safe — тянуть все remotes (<c>fetch --all</c>) и сбрасываться на upstream
/// текущей ветки (<c>@{u}</c>), с откатом на <c>origin/{branch}</c>, если upstream не настроен.
/// </summary>
public class LocalGitWorkspaceSyncTests
{
    private const string Workspace = "/tmp/ws";

    [Fact(DisplayName = "Sync: fetch --prune --all и reset --hard на upstream @{u}")]
    public async Task Sync_fetches_all_and_resets_to_upstream()
    {
        var fx = new SyncFixture(branch: "feature", upstream: "origin/feature");

        await fx.Sync.SyncCurrentBranchToRemoteAsync(Workspace, default);

        fx.ArgsOf("fetch").Should().BeEquivalentTo(
            ["-C", Workspace, "fetch", "--prune", "--all"], o => o.WithStrictOrdering());
        fx.ArgsOf("reset").Should().BeEquivalentTo(
            ["-C", Workspace, "reset", "--hard", "origin/feature"], o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "Sync (fork-PR): @{u} = remote форка → reset --hard на него, не на origin")]
    public async Task Sync_fork_pr_resets_to_fork_remote_upstream()
    {
        var fx = new SyncFixture(branch: "feature", upstream: "fork/feature");

        await fx.Sync.SyncCurrentBranchToRemoteAsync(Workspace, default);

        fx.ArgsOf("reset").Should().BeEquivalentTo(
            ["-C", Workspace, "reset", "--hard", "fork/feature"], o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "Sync: upstream не настроен (@{u} даёт ненулевой код) → fallback origin/{branch}")]
    public async Task Sync_without_upstream_falls_back_to_origin_branch()
    {
        var fx = new SyncFixture(branch: "feature", upstream: null);

        await fx.Sync.SyncCurrentBranchToRemoteAsync(Workspace, default);

        fx.ArgsOf("reset").Should().BeEquivalentTo(
            ["-C", Workspace, "reset", "--hard", "origin/feature"], o => o.WithStrictOrdering());
    }

    private sealed class SyncFixture
    {
        public SyncFixture(string branch, string? upstream)
        {
            var launcher = Substitute.For<IProcessLauncher>();
            launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var req = ci.Arg<ProcessRunRequest>();
                    Calls.Add(req);
                    if (req.Arguments.Contains("@{u}"))
                    {
                        return Task.FromResult(upstream is null ? Fail() : Ok(upstream));
                    }
                    if (req.Arguments.Contains("--abbrev-ref") && req.Arguments.Contains("HEAD"))
                    {
                        return Task.FromResult(Ok(branch));
                    }
                    return Task.FromResult(Ok(string.Empty));
                });
            Sync = new LocalGitWorkspaceSync(launcher);
        }

        public LocalGitWorkspaceSync Sync { get; }

        public List<ProcessRunRequest> Calls { get; } = [];

        public IReadOnlyList<string> ArgsOf(string verb) =>
            Calls.Single(c => c.Arguments.Contains(verb) && !c.Arguments.Contains("@{u}")).Arguments;

        private static ProcessRunResult Ok(string stdout) =>
            new(ExitCode: 0, StandardOutput: stdout, StandardError: string.Empty, Elapsed: TimeSpan.Zero);

        private static ProcessRunResult Fail() =>
            new(ExitCode: 128, StandardOutput: string.Empty, StandardError: "no upstream", Elapsed: TimeSpan.Zero);
    }
}
