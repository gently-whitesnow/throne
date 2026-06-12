using FluentAssertions;
using Throne.Domain.Intents;

namespace Throne.Domain.Tests.Intents;

public class IntentStatusNamesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "IsKnown пропускает awaiting_operator")]
    public void IsKnown_accepts_awaiting_operator()
    {
        IntentStatusNames.IsKnown(IntentStatusNames.AwaitingOperator).Should().BeTrue();
        IntentStatusNames.All.Should().Contain(IntentStatusNames.AwaitingOperator);
    }

    [Fact(DisplayName = "IsKnown пропускает fridge")]
    public void IsKnown_accepts_fridge()
    {
        IntentStatusNames.IsKnown(IntentStatusNames.Fridge).Should().BeTrue();
        IntentStatusNames.All.Should().Contain(IntentStatusNames.Fridge);
    }

    [Fact(DisplayName = "SetStatus принимает awaiting_operator")]
    public void SetStatus_accepts_awaiting_operator()
    {
        var intent = Intent.Create(IntentId.New(), "hello", tagIds: null, Now);

        intent.SetStatus(IntentStatusNames.AwaitingOperator, Now.AddMinutes(5)).Should().BeTrue();
        intent.State.Status.Should().Be(IntentStatusNames.AwaitingOperator);
    }

    [Fact(DisplayName = "SetStatus принимает fridge")]
    public void SetStatus_accepts_fridge()
    {
        var intent = Intent.Create(IntentId.New(), "hello", tagIds: null, Now);

        intent.SetStatus(IntentStatusNames.Fridge, Now.AddMinutes(5)).Should().BeTrue();
        intent.State.Status.Should().Be(IntentStatusNames.Fridge);
    }
}
