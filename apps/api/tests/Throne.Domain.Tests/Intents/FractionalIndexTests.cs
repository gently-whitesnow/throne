using FluentAssertions;
using Throne.Domain.Intents;

namespace Throne.Domain.Tests.Intents;

public class FractionalIndexTests
{
    [Fact(DisplayName = "Initial возвращает середину алфавита и не оканчивается на минимальный символ")]
    public void Initial_returns_midpoint()
    {
        var key = FractionalIndex.Initial();
        key.Should().NotBeEmpty();
        key.Should().NotEndWith("0");
    }

    [Fact(DisplayName = "Between корректно обрабатывает все 4 базовых случая (null/null, prepend, append, mid)")]
    public void Between_respects_bounds()
    {
        FractionalIndex.Between(null, null).Should().Be(FractionalIndex.Initial());

        var prepend = FractionalIndex.Between(null, "V");
        FractionalIndexAssertions.AssertStrictlyInside(prepend, lower: null, upper: "V");

        var append = FractionalIndex.Between("V", null);
        FractionalIndexAssertions.AssertStrictlyInside(append, lower: "V", upper: null);

        var midpoint = FractionalIndex.Between("A", "B");
        FractionalIndexAssertions.AssertStrictlyInside(midpoint, lower: "A", upper: "B");
    }

    [Fact(DisplayName = "Между смежными ключами всегда есть промежуточный — N итераций без коллизий")]
    public void Repeated_subdivision_remains_strict()
    {
        var lo = "A";
        var hi = "B";
        for (var i = 0; i < 50; i++)
        {
            var mid = FractionalIndex.Between(lo, hi);
            string.CompareOrdinal(lo, mid).Should().BeLessThan(0, $"iter {i}: lo={lo} mid={mid}");
            string.CompareOrdinal(mid, hi).Should().BeLessThan(0, $"iter {i}: mid={mid} hi={hi}");
            mid.Should().NotEndWith("0");
            // Drift toward the lower side to exercise narrowing in both directions.
            if (i % 2 == 0)
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }
    }

    [Fact(DisplayName = "GenerateAscending(N) возвращает строго возрастающие ключи")]
    public void Generate_ascending_is_strictly_increasing()
    {
        var keys = FractionalIndex.GenerateAscending(20);
        keys.Should().HaveCount(20);
        for (var i = 1; i < keys.Count; i++)
        {
            string.CompareOrdinal(keys[i - 1], keys[i]).Should().BeLessThan(0);
            keys[i].Should().NotEndWith("0");
        }
    }

    [Fact(DisplayName = "Between(a, b) при a >= b выбрасывает ArgumentException")]
    public void Between_rejects_inverted_pair()
    {
        var act = () => FractionalIndex.Between("B", "A");
        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "ValidateKey запрещает пустые ключи и trailing '0'")]
    [InlineData("")]
    [InlineData("A0")]
    public void ValidateKey_rejects_invalid(string key)
    {
        var act = () => FractionalIndex.ValidateKey(key);
        act.Should().Throw<ArgumentException>();
    }
}
