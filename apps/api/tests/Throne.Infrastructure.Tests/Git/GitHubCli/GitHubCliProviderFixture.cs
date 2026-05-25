using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Shared assembly of <see cref="GitHubCliProvider"/> + a mocked
/// <see cref="IProcessLauncher"/> for the per-method test classes. Records every
/// <see cref="ProcessRunRequest"/> on <see cref="Calls"/> so tests can assert
/// command lines without re-deriving them.
/// </summary>
internal sealed class GitHubCliProviderFixture
{
    public GitHubCliProviderFixture()
    {
        Launcher = Substitute.For<IProcessLauncher>();
        var options = Options.Create(new GitHubCliOptions { ExecutablePath = "gh", PageSize = 50 });
        var invoker = new GhCliInvoker(Launcher, options);
        var listExec = new GhRepoListExecutor(invoker);
        var searcher = new GhRepoSearcher(invoker, listExec);
        var actions = new GhRepoActions(invoker);
        var probe = new GhAuthProbe(invoker);
        var prActions = new GhPullRequestActions(invoker);
        var refListers = new GhRefListers(new GhBranchLister(invoker), new GhPullRequestLister(invoker));
        Provider = new GitHubCliProvider(searcher, actions, probe, prActions, refListers);
    }

    public IProcessLauncher Launcher { get; }

    public GitHubCliProvider Provider { get; }

    public List<ProcessRunRequest> Calls { get; } = new();

    public void OnRun(Func<ProcessRunRequest, ProcessRunResult> factory)
    {
        Launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<ProcessRunRequest>();
                Calls.Add(req);
                return Task.FromResult(factory(req));
            });
    }

    public static ProcessRunResult Ok(string stdout) =>
        new(ExitCode: 0, StandardOutput: stdout, StandardError: string.Empty, Elapsed: TimeSpan.Zero);

    public static ProcessRunResult Fail(int exit, string stderr) =>
        new(ExitCode: exit, StandardOutput: string.Empty, StandardError: stderr, Elapsed: TimeSpan.Zero);

    public static bool IsApiCall(ProcessRunRequest req) =>
        req.Arguments.Count > 0 && req.Arguments[0] == "api";
}
