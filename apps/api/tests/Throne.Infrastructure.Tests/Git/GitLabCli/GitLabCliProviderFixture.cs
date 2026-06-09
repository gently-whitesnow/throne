using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

internal sealed class GitLabCliProviderFixture
{
    public GitLabCliProviderFixture()
    {
        Launcher = Substitute.For<IProcessLauncher>();
        var settings = Options.Create(new GitLabSettings { Host = Host });
        var options = Options.Create(new GitLabCliOptions
        {
            ExecutablePath = "glab",
            GitExecutablePath = "git",
            PageSize = 50,
        });
        var invoker = new GlabCliInvoker(Launcher, options);
        var searcher = new GlabRepoSearcher(invoker, settings);
        var actions = new GlabRepoActions(invoker, settings, options, Launcher);
        var probe = new GlabAuthProbe(invoker, settings);
        var prActions = new GlabPullRequestActions(invoker, settings);
        var refListers = new GlabRefListers(new GlabBranchLister(invoker, settings), new GlabPullRequestLister(invoker, settings));
        var reviewWorkspace = new GlabReviewWorkspaceActions(invoker, settings);
        Provider = new GitLabCliProvider(searcher, actions, probe, prActions, refListers, reviewWorkspace);
    }

    public const string Host = "gitlab.example.com";

    public IProcessLauncher Launcher { get; }

    public GitLabCliProvider Provider { get; }

    public List<ProcessRunRequest> Calls { get; } = [];

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

    public static bool HasGitLabHost(ProcessRunRequest req) =>
        req.Environment is not null
        && req.Environment.TryGetValue("GITLAB_HOST", out var host)
        && host == Host;
}
