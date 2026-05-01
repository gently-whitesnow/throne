using FluentAssertions;

namespace Throne.Domain.Tests;

public class SmokeTests
{
    [Fact]
    public void Domain_assembly_loads()
    {
        typeof(Throne.Domain.DomainAssemblyMarker).Assembly
            .GetName().Name.Should().Be("Throne.Domain");
    }
}
