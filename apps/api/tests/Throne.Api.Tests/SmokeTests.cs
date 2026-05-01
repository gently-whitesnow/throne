using FluentAssertions;

namespace Throne.Api.Tests;

public class SmokeTests
{
    [Fact]
    public void Api_assembly_loads()
    {
        typeof(Program).Assembly
            .GetName().Name.Should().Be("Throne.Api");
    }
}
