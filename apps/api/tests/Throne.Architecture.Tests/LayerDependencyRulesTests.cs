using FluentAssertions;
using NetArchTest.Rules;

namespace Throne.Architecture.Tests;

/// <summary>
/// Clean Architecture layer rules for Throne (ADR-0001).
///
/// Allowed direction:
///   Api ──► Application ──► Domain
///   Infrastructure ──► Application ──► Domain
///   Api ──► Infrastructure (DI wiring only)
/// Whitelist rules (DomainAllowedRoots / ApplicationAllowedRoots) catch the
/// "agent silently dragged a new NuGet into Domain" failure mode.
/// </summary>
public class LayerDependencyRulesTests
{
    private const string Domain = "Throne.Domain";
    private const string Application = "Throne.Application";
    private const string Infrastructure = "Throne.Infrastructure";
    private const string Api = "Throne.Api";

    private static readonly System.Reflection.Assembly DomainAsm =
        typeof(Throne.Domain.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAsm =
        typeof(Throne.Application.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAsm =
        typeof(Throne.Infrastructure.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly ApiAsm =
        typeof(Throne.Api.AssemblyMarker).Assembly;

    // Anything that starts with one of these namespace prefixes is allowed.
    // Adding a new package to Domain/Application is a deliberate architectural
    // decision and must extend this list together with an ADR or rationale.
    private static readonly string[] DomainAllowedRoots =
    [
        "System",
        "Throne.Domain",
    ];

    private static readonly string[] ApplicationAllowedRoots =
    [
        "System",
        "Microsoft.Extensions",
        "Throne.Application",
        "Throne.Domain",
        "YamlDotNet",
    ];

    [Fact(DisplayName = "Domain не зависит от других слоёв")]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types
            .InAssembly(DomainAsm)
            .Should()
            .NotHaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Domain нарушил направление зависимостей. " +
            "Если домену нужен внешний контракт — объяви интерфейс прямо в Throne.Domain " +
            "и реализуй его в Throne.Application/Infrastructure. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Application не зависит от Infrastructure / Api")]
    public void Application_should_not_depend_on_outer_layers()
    {
        var result = Types
            .InAssembly(ApplicationAsm)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Application зависит от внешнего слоя. " +
            "Use case должен общаться с миром через порт (интерфейс) в Throne.Application/Ports, " +
            "реализация — в Throne.Infrastructure. См. specs/AGENTS.local.md → Архитектурные слои. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Infrastructure не зависит от Api")]
    public void Infrastructure_should_not_depend_on_presentation_layers()
    {
        var result = Types
            .InAssembly(InfrastructureAsm)
            .Should()
            .NotHaveDependencyOnAny(Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Infrastructure зависит от слоя транспорта. " +
            "Если нужно общее — вынеси в Throne.Application. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Domain whitelist: только System и Throne.Domain")]
    public void Domain_should_only_depend_on_allowlist()
    {
        // Scope to Throne types — collection expressions emit anonymous-namespace
        // helpers (<>z__ReadOnlyArray<T>, nested Enumerator) which are noise here.
        var result = Types
            .InAssembly(DomainAsm)
            .That()
            .ResideInNamespaceStartingWith("Throne")
            .Should()
            .OnlyHaveDependenciesOn(DomainAllowedRoots)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Domain потащил новую внешнюю зависимость. Whitelist: {0}. " +
            "Если зависимость нужна — обнови DomainAllowedRoots в этом тесте + добавь ADR. " +
            "Failing types: {1}",
            string.Join(", ", DomainAllowedRoots),
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Application whitelist: System / Microsoft.Extensions / Throne.* / YamlDotNet")]
    public void Application_should_only_depend_on_allowlist()
    {
        var result = Types
            .InAssembly(ApplicationAsm)
            .That()
            .ResideInNamespaceStartingWith("Throne")
            .Should()
            .OnlyHaveDependenciesOn(ApplicationAllowedRoots)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Application потащил новую внешнюю зависимость. Whitelist: {0}. " +
            "Если зависимость нужна — обнови ApplicationAllowedRoots в этом тесте + добавь ADR. " +
            "Failing types: {1}",
            string.Join(", ", ApplicationAllowedRoots),
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
