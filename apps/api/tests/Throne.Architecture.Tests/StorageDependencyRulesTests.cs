using FluentAssertions;
using NetArchTest.Rules;

namespace Throne.Architecture.Tests;

public class StorageDependencyRulesTests
{
    private static readonly System.Reflection.Assembly[] BackendAssemblies =
    [
        typeof(Throne.Domain.AssemblyMarker).Assembly,
        typeof(Throne.Application.AssemblyMarker).Assembly,
        typeof(Throne.Infrastructure.AssemblyMarker).Assembly,
        typeof(Throne.Api.AssemblyMarker).Assembly,
    ];

    [Fact(DisplayName = "Backend assemblies не зависят от MongoDB driver namespaces")]
    public void Backend_should_not_depend_on_mongodb_driver()
    {
        var result = Types
            .InAssemblies(BackendAssemblies)
            .That()
            .ResideInNamespaceStartingWith("Throne")
            .Should()
            .NotHaveDependencyOn("MongoDB")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "MongoDB driver dependency was removed with the SQLite/EF Core persistence decision. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
