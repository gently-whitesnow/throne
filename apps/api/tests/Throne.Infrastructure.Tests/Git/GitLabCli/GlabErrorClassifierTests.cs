using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GlabErrorClassifierTests
{
    [Theory(DisplayName = "Classify распознаёт NetworkError на сетевых ошибках и 5xx")]
    [InlineData("HTTP 502: Bad Gateway")]
    [InlineData("dial tcp 10.0.0.1:443: connect: connection refused")]
    [InlineData("i/o timeout")]
    [InlineData("x509: certificate signed by unknown authority")]
    [InlineData("proxyconnect tcp: tls: handshake failure")]
    [InlineData("network is unreachable")]
    public void Classifies_network_errors(string stderr) =>
        GlabErrorClassifier.Classify(stderr).Should().Be(GitProviderErrorKind.NetworkError);

    [Theory(DisplayName = "Classify распознаёт NotFound/AuthFailed")]
    [InlineData("HTTP 404: 404 Project Not Found", GitProviderErrorKind.NotFound)]
    [InlineData("HTTP 401: Unauthorized", GitProviderErrorKind.AuthFailed)]
    [InlineData("authentication required; run glab auth login", GitProviderErrorKind.AuthFailed)]
    public void Classifies_known_non_network_errors(string stderr, GitProviderErrorKind expected) =>
        GlabErrorClassifier.Classify(stderr).Should().Be(expected);
}
