using FluentAssertions;

namespace Throne.Domain.Tests.Intents;

internal static class FractionalIndexAssertions
{
    public static void AssertStrictlyInside(string key, string? lower, string? upper)
    {
        key.Should().NotBeEmpty();
        key.Should().NotEndWith("0");
        if (lower is not null)
        {
            string.CompareOrdinal(lower, key).Should().BeLessThan(0);
        }
        if (upper is not null)
        {
            string.CompareOrdinal(key, upper).Should().BeLessThan(0);
        }
    }
}
