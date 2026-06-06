using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

/// <summary>
/// Pure helpers around <see cref="RunPreflightWaitResult"/> + binding lists, used by
/// <see cref="RunPreflightOrchestrator"/>.
/// </summary>
internal static class RunPreflightSession
{
    public static void EnsureWaitDidNotTimeOut(string intentId, RunPreflightWaitResult waitResult)
    {
        if (!waitResult.TimedOut)
        {
            return;
        }

        var pending = waitResult.Bindings
            .Where(b => b.State.CloneStatus is CloneStatusNames.Pending or CloneStatusNames.Cloning)
            .Select(b => b.Id.Value)
            .ToArray();
        throw TerminalFailures.CloneWaitTimeout(intentId, waitResult.WaitedSeconds, pending);
    }

    public static IReadOnlyList<string> CollectBlocking(IReadOnlyList<IntentRepositoryBinding> bindings) =>
        bindings
            .Where(b => b.State.CloneStatus is CloneStatusNames.Failed or CloneStatusNames.Broken)
            .Select(b => b.Id.Value)
            .ToArray();

    public static RunPreflightResult BuildResult(
        string intentId,
        string sessionName,
        string sessionState,
        IReadOnlyList<IntentRepositoryBinding> bindings,
        IReadOnlyList<string> blockingBindings) =>
        new(
            IntentId: intentId,
            SessionName: sessionName,
            SessionState: sessionState,
            Bindings: bindings.Select(RunPreflightBindingMapper.ToStatus).ToArray(),
            BlockingBindings: blockingBindings);
}
