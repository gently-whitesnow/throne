using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhErrorClassifierTests
{
    [Theory(DisplayName = "Classify распознаёт NotFound по сигнальным строкам")]
    [InlineData("HTTP 404: Not Found (https://api.github.com/repos/x/y)")]
    [InlineData("Could not resolve to a Repository with the name 'x/y'.")]
    public void Classifies_not_found(string stderr) =>
        GhErrorClassifier.Classify(stderr).Should().Be(GitProviderErrorKind.NotFound);

    [Theory(DisplayName = "Classify распознаёт AuthFailed для 401/403/Bad credentials")]
    [InlineData("HTTP 401: Bad credentials")]
    [InlineData("HTTP 403: API rate limit exceeded\nYou are not authorized")]
    [InlineData("error: not logged into github.com. Run gh auth login")]
    public void Classifies_auth_failed(string stderr) =>
        GhErrorClassifier.Classify(stderr).Should().Be(GitProviderErrorKind.AuthFailed);

    [Theory(DisplayName = "Classify распознаёт NetworkError на сетевых ошибках и 5xx")]
    [InlineData("HTTP 503: Service Unavailable")]
    [InlineData("Could not resolve host: api.github.com")]
    [InlineData("rate limit exceeded")]
    [InlineData("connection refused")]
    public void Classifies_network(string stderr) =>
        GhErrorClassifier.Classify(stderr).Should().Be(GitProviderErrorKind.NetworkError);

    [Fact(DisplayName = "Classify валит пустой stderr в CliFailure")]
    public void Empty_stderr_falls_back_to_cli_failure() =>
        GhErrorClassifier.Classify(string.Empty).Should().Be(GitProviderErrorKind.CliFailure);

    [Fact(DisplayName = "Classify валит неизвестный stderr в CliFailure")]
    public void Unknown_stderr_falls_back_to_cli_failure() =>
        GhErrorClassifier.Classify("something weird happened").Should().Be(GitProviderErrorKind.CliFailure);

    [Fact(DisplayName = "OneLine схлопывает переводы строк и обрезает по длине")]
    public void OneLine_collapses_and_truncates()
    {
        var stderr = new string('a', 300) + "\nfoo\r\nbar";

        var line = GhErrorClassifier.OneLine(stderr);

        line.Should().NotContain("\n");
        line.Should().NotContain("\r");
        line.Should().EndWith("...");
        line.Length.Should().BeLessThanOrEqualTo(240);
    }
}
