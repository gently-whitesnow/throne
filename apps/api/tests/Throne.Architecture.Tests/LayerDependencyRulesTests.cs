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
///
/// Mcp.Stdio is an intentionally thin STDIO→HTTP MCP proxy with NO domain
/// knowledge (ADR-0009). It must not pull Domain / Application / Infrastructure /
/// Api into its process — otherwise domain events fire in the proxy and SSE
/// subscribers in apps/web never see them.
///
/// Whitelist rules (DomainAllowedRoots / ApplicationAllowedRoots) catch the
/// "agent silently dragged a new NuGet into Domain" failure mode.
/// </summary>
public class LayerDependencyRulesTests
{
    private const string Domain = "Throne.Domain";
    private const string Application = "Throne.Application";
    private const string Infrastructure = "Throne.Infrastructure";
    private const string Api = "Throne.Api";
    private const string McpStdio = "Throne.Mcp.Stdio";

    private static readonly System.Reflection.Assembly DomainAsm =
        typeof(Throne.Domain.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAsm =
        typeof(Throne.Application.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAsm =
        typeof(Throne.Infrastructure.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly ApiAsm =
        typeof(Throne.Api.AssemblyMarker).Assembly;
    private static readonly System.Reflection.Assembly McpStdioAsm =
        typeof(Throne.Mcp.Stdio.AssemblyMarker).Assembly;

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
            .NotHaveDependencyOnAny(Application, Infrastructure, Api, McpStdio)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Domain нарушил направление зависимостей. " +
            "Если домену нужен внешний контракт — объяви интерфейс прямо в Throne.Domain " +
            "и реализуй его в Throne.Application/Infrastructure. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Application не зависит от Infrastructure / Api / Mcp.Stdio")]
    public void Application_should_not_depend_on_outer_layers()
    {
        var result = Types
            .InAssembly(ApplicationAsm)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api, McpStdio)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Application зависит от внешнего слоя. " +
            "Use case должен общаться с миром через порт (интерфейс) в Throne.Application/Ports, " +
            "реализация — в Throne.Infrastructure. См. specs/AGENTS.local.md → Архитектурные слои. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Infrastructure не зависит от Api / Mcp.Stdio")]
    public void Infrastructure_should_not_depend_on_presentation_layers()
    {
        var result = Types
            .InAssembly(InfrastructureAsm)
            .Should()
            .NotHaveDependencyOnAny(Api, McpStdio)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Infrastructure зависит от слоя транспорта. " +
            "Если нужно общее — вынеси в Throne.Application. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Mcp.Stdio — тонкий proxy, без Domain/Application/Infrastructure/Api")]
    public void McpStdio_must_not_depend_on_domain_or_api()
    {
        var result = Types
            .InAssembly(McpStdioAsm)
            .Should()
            .NotHaveDependencyOnAny(Domain, Application, Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Throne.Mcp.Stdio должен оставаться тонким STDIO→HTTP proxy (ADR-0009). " +
            "Любая ссылка на Throne.Domain/Application/Infrastructure/Api ломает изоляцию: " +
            "domain events начнут срабатывать в этом процессе и SSE-подписчики apps/web " +
            "перестанут их видеть. Failing types: {0}",
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
