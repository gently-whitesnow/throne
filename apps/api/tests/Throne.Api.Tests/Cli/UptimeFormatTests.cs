using FluentAssertions;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Cli;

public class UptimeFormatTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Seconds_are_rendered_alone()
    {
        UptimeFormat.Describe(Now.AddSeconds(-42), Now).Should().Be("42s");
    }

    [Fact]
    public void Minutes_carry_zero_padded_seconds()
    {
        UptimeFormat.Describe(Now.AddMinutes(-13).AddSeconds(-7), Now).Should().Be("13m 07s");
    }

    [Fact]
    public void Hours_carry_zero_padded_minutes()
    {
        UptimeFormat.Describe(Now.AddHours(-5).AddMinutes(-4), Now).Should().Be("5h 04m");
    }

    [Fact]
    public void Days_carry_hours()
    {
        UptimeFormat.Describe(Now.AddDays(-2).AddHours(-4), Now).Should().Be("2d 4h");
    }
}
