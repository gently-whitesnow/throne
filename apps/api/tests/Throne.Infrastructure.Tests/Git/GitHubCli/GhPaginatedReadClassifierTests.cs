using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhPaginatedReadClassifierTests
{
    [Theory(DisplayName = "Classify относит rate-limit stderr к RateLimited")]
    [InlineData("gh: API rate limit exceeded for user ID 1 (HTTP 403)")]
    [InlineData("You have exceeded a secondary rate limit. Please wait a few minutes.")]
    public void Classify_rate_limit_to_rate_limited(string stderr)
    {
        GhPaginatedReadClassifier.Classify(stderr).Should().Be(GhReadFailureKind.RateLimited);
    }

    [Theory(DisplayName = "Classify относит 404 stderr к NotFound")]
    [InlineData("gh: Not Found (HTTP 404)")]
    [InlineData("HTTP 404: this page could not be found")]
    public void Classify_404_to_not_found(string stderr)
    {
        GhPaginatedReadClassifier.Classify(stderr).Should().Be(GhReadFailureKind.NotFound);
    }

    [Theory(DisplayName = "Classify относит прочее (и пустое) к Other")]
    [InlineData("")]
    [InlineData("gh: HTTP 500 Internal Server Error")]
    public void Classify_other(string stderr)
    {
        GhPaginatedReadClassifier.Classify(stderr).Should().Be(GhReadFailureKind.Other);
    }
}
