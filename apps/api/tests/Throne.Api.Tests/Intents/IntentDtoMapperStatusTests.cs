using FluentAssertions;
using Throne.Api.Intents;
using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Tests.Intents;

public class IntentDtoMapperStatusTests
{
    [Fact(DisplayName = "round-trip domain → contract → domain покрывает все IntentStatusNames.All")]
    public void Round_trip_covers_every_known_status()
    {
        foreach (var domain in IntentStatusNames.All)
        {
            var contract = IntentStatusDtoMapper.ToContractStatus(domain);
            var back = IntentStatusDtoMapper.FromContractStatus(contract);
            back.Should().Be(domain, "round-trip должен сохранять статус '{0}'", domain);
        }
    }

    [Fact(DisplayName = "FromContractStatus покрывает все значения IntentStatus enum")]
    public void From_contract_covers_every_enum_value()
    {
        foreach (var value in Enum.GetValues<IntentStatus>())
        {
            var act = () => IntentStatusDtoMapper.FromContractStatus(value);
            act.Should().NotThrow($"contract IntentStatus.{value} должен иметь обратный маппер в домен");
        }
    }
}
