using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

public class GitProviderRegistryTests
{
    [Fact(DisplayName = "GetByName возвращает зарегистрированного провайдера")]
    public void Resolves_registered_provider()
    {
        var github = Substitute.For<IGitProvider>();
        github.ProviderName.Returns("github");

        var registry = new GitProviderRegistry([github]);

        registry.GetByName("github").Should().BeSameAs(github);
        registry.AllProviders.Should().ContainSingle().Which.Should().BeSameAs(github);
    }

    [Fact(DisplayName = "GetByName возвращает null для неизвестного провайдера")]
    public void Returns_null_for_unknown()
    {
        var registry = new GitProviderRegistry([]);

        registry.GetByName("gitlab").Should().BeNull();
        registry.AllProviders.Should().BeEmpty();
    }

    [Fact(DisplayName = "Дубликат регистрации проваливается с InvalidOperationException")]
    public void Duplicate_registration_throws()
    {
        var a = Substitute.For<IGitProvider>();
        a.ProviderName.Returns("github");
        var b = Substitute.For<IGitProvider>();
        b.ProviderName.Returns("github");

        var act = () => new GitProviderRegistry([a, b]);

        act.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains("github", StringComparison.Ordinal));
    }
}
