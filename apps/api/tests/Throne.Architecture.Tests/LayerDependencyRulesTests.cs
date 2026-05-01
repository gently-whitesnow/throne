using FluentAssertions;
using NetArchTest.Rules;

namespace Throne.Architecture.Tests;

public class LayerDependencyRulesTests
{
    private const string DomainAssembly = "Throne.Domain";
    private const string ApplicationAssembly = "Throne.Application";
    private const string InfrastructureAssembly = "Throne.Infrastructure";
    private const string ApiAssembly = "Throne.Api";

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types
            .InAssembly(typeof(Throne.Domain.Intents.Intent).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationAssembly, InfrastructureAssembly, ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Domain must not depend on any other layer. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types
            .InAssembly(typeof(Throne.Application.Ports.IIntentRepository).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureAssembly, ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Application must not depend on Infrastructure/Api. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types
            .InAssembly(typeof(Throne.Infrastructure.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Infrastructure must not depend on Api. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
