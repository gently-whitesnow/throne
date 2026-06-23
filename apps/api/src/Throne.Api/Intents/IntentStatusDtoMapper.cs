using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentStatusDtoMapper
{
    public static IntentStatus ToContractStatus(string status) => status switch
    {
        IntentStatusNames.Draft => IntentStatus.Draft,
        IntentStatusNames.Interview => IntentStatus.Interview,
        IntentStatusNames.ReadyForWork => IntentStatus.Ready_for_work,
        IntentStatusNames.Work => IntentStatus.Work,
        IntentStatusNames.AwaitingOperator => IntentStatus.Awaiting_operator,
        IntentStatusNames.Done => IntentStatus.Done,
        IntentStatusNames.Reject => IntentStatus.Reject,
        IntentStatusNames.Fridge => IntentStatus.Fridge,
        _ => throw new InvalidOperationException($"Unknown domain status: {status}"),
    };

    public static string FromContractStatus(IntentStatus status) => status switch
    {
        IntentStatus.Draft => IntentStatusNames.Draft,
        IntentStatus.Interview => IntentStatusNames.Interview,
        IntentStatus.Ready_for_work => IntentStatusNames.ReadyForWork,
        IntentStatus.Work => IntentStatusNames.Work,
        IntentStatus.Awaiting_operator => IntentStatusNames.AwaitingOperator,
        IntentStatus.Done => IntentStatusNames.Done,
        IntentStatus.Reject => IntentStatusNames.Reject,
        IntentStatus.Fridge => IntentStatusNames.Fridge,
        _ => throw new InvalidOperationException($"Unknown contract status: {status}"),
    };
}
