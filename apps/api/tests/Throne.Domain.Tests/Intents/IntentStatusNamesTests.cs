using FluentAssertions;
using Throne.Domain.Intents;

namespace Throne.Domain.Tests.Intents;

public class IntentStatusNamesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "IsKnown пропускает needs_help")]
    public void IsKnown_accepts_needs_help()
    {
        IntentStatusNames.IsKnown(IntentStatusNames.NeedsHelp).Should().BeTrue();
        IntentStatusNames.All.Should().Contain(IntentStatusNames.NeedsHelp);
    }

    [Fact(DisplayName = "IsKnown пропускает fridge")]
    public void IsKnown_accepts_fridge()
    {
        IntentStatusNames.IsKnown(IntentStatusNames.Fridge).Should().BeTrue();
        IntentStatusNames.All.Should().Contain(IntentStatusNames.Fridge);
    }

    [Fact(DisplayName = "SetStatus принимает needs_help")]
    public void SetStatus_accepts_needs_help()
    {
        var intent = Intent.Create(IntentId.New(), "user-1", "hello", tagIds: null, Now);

        intent.SetStatus(IntentStatusNames.NeedsHelp, Now.AddMinutes(5)).Should().BeTrue();
        intent.Status.Should().Be(IntentStatusNames.NeedsHelp);
    }

    [Fact(DisplayName = "SetStatus принимает fridge")]
    public void SetStatus_accepts_fridge()
    {
        var intent = Intent.Create(IntentId.New(), "user-1", "hello", tagIds: null, Now);

        intent.SetStatus(IntentStatusNames.Fridge, Now.AddMinutes(5)).Should().BeTrue();
        intent.Status.Should().Be(IntentStatusNames.Fridge);
    }
}
