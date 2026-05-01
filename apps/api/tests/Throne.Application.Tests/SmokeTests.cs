using FluentAssertions;
using Throne.Application.Ports;

namespace Throne.Application.Tests;

public class SmokeTests
{
    [Fact]
    public void Ports_are_defined()
    {
        typeof(IIntentRepository).IsInterface.Should().BeTrue();
        typeof(IInstructionRepository).IsInterface.Should().BeTrue();
    }
}
