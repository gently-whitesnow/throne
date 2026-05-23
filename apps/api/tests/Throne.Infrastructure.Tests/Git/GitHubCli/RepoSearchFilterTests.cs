using FluentAssertions;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class RepoSearchFilterTests
{
    [Fact(DisplayName = "Apply без запроса возвращает первые limit записей")]
    public void No_query_caps_to_limit()
    {
        var repos = new[] { Repo("a"), Repo("b"), Repo("c") };

        RepoSearchFilter.Apply(repos, query: null, limit: 2)
            .Should().HaveCount(2)
            .And.ContainInOrder(repos[0], repos[1]);
    }

    [Fact(DisplayName = "Apply фильтрует по подстроке в name, fullname и description")]
    public void Query_matches_substring_case_insensitively()
    {
        var repos = new[]
        {
            Repo("throne-api", description: "knowledge base"),
            Repo("other", owner: "throne-org"),
            Repo("misc", description: "Tools for ThRoNe"),
            Repo("unrelated"),
        };

        var hits = RepoSearchFilter.Apply(repos, query: "throne", limit: 10);

        hits.Should().HaveCount(3);
        hits.Should().NotContain(r => r.Repo == "unrelated");
    }

    [Fact(DisplayName = "Apply с limit=0 возвращает все совпадения")]
    public void Limit_zero_means_all()
    {
        var repos = new[] { Repo("a"), Repo("b"), Repo("c") };

        RepoSearchFilter.Apply(repos, query: null, limit: 0).Should().HaveCount(3);
    }

    private static GitRepositoryRef Repo(string repo, string owner = "alice", string? description = null) =>
        new(GitProviderNames.GitHub, owner, repo, "main") { Description = description };
}
