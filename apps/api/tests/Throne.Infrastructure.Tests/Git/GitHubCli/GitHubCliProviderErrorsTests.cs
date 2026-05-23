using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Error-mapping branches of <see cref="Throne.Infrastructure.Git.GitHubCli.GitHubCliProvider"/>.
/// Asserts that non-zero <c>gh</c> exits and transport failures surface as
/// typed <see cref="GitProviderException"/>s with the right
/// <see cref="GitProviderErrorKind"/> per ADR-0024 § 5/7. Separate from
/// <see cref="GitHubCliProviderTests"/> so each class stays inside the
/// maintainability budget.
/// </summary>
public class GitHubCliProviderErrorsTests
{
    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "Clone мапит HTTP 404 → GitProviderException(NotFound)")]
    public async Task Clone_maps_404_to_not_found_exception()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Fail(exit: 1, stderr: "HTTP 404: Not Found (repos/x/y)"));

        var act = async () => await _fx.Provider.CloneRepositoryAsync("x", "y", "/tmp/x", default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NotFound);
    }

    [Fact(DisplayName = "Clone мапит HTTP 401 → GitProviderException(AuthFailed)")]
    public async Task Clone_maps_401_to_auth_failed_exception()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Fail(exit: 1, stderr: "HTTP 401: Bad credentials"));

        var act = async () => await _fx.Provider.CloneRepositoryAsync("x", "y", "/tmp/x", default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.AuthFailed);
    }

    [Fact(DisplayName = "Clone преобразует TimeoutException в NetworkError")]
    public async Task Clone_maps_timeout_to_network_error()
    {
        _fx.Launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("slow"));

        var act = async () => await _fx.Provider.CloneRepositoryAsync("x", "y", "/tmp/x", default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NetworkError);
    }

    [Fact(DisplayName = "Fetch мапит сетевую ошибку gh repo sync → NetworkError")]
    public async Task Fetch_maps_network_failure()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Fail(exit: 1, stderr: "Could not resolve host: github.com"));

        var act = async () => await _fx.Provider.FetchRepositoryAsync("/tmp/ws", default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NetworkError);
    }

    [Fact(DisplayName = "PR-методы бросают NotSupportedException до T-07")]
    public async Task Pr_methods_are_not_implemented_yet()
    {
        await ((Func<Task>)(() => _fx.Provider.GetPullRequestAsync("a", "b", 1, default)))
            .Should().ThrowAsync<NotSupportedException>();
        await ((Func<Task>)(() => _fx.Provider.ListPullRequestCommentsAsync("a", "b", 1, etag: null, default)))
            .Should().ThrowAsync<NotSupportedException>();
    }
}
