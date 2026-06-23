using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

/// <summary>
/// Pure helpers around <see cref="RunPreflightWaitResult"/> + binding lists. Split out of
/// <see cref="RunPreflightOrchestrator"/> so the orchestrator type-level cyclomatic
/// stays under the project-wide CA1502 budget.
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

    /// <summary>
    /// Absolute clone paths of the intent's ready repos — the workspace map handed to the agent so
    /// it reads real paths instead of guessing the clone sub-dir name. Non-ready bindings already
    /// blocked the spawn, so in practice this is every binding; the filter keeps it honest.
    /// </summary>
    public static IReadOnlyList<string> CollectReadyRepoPaths(IReadOnlyList<IntentRepositoryBinding> bindings) =>
        bindings
            .Where(b => b.State.CloneStatus is CloneStatusNames.Ready)
            .Select(b => b.WorkspacePath)
            .ToArray();

    public static RunPreflightResult BuildResult(
        string intentId,
        string sessionName,
        string sessionState,
        IReadOnlyList<IntentRepositoryBinding> bindings,
        IReadOnlyList<string> blockingBindings,
        TerminalLaunchRecord? launch = null) =>
        new(
            IntentId: intentId,
            SessionName: sessionName,
            SessionState: sessionState,
            Bindings: bindings.Select(RunPreflightBindingMapper.ToStatus).ToArray(),
            BlockingBindings: blockingBindings,
            Launch: launch);
}
