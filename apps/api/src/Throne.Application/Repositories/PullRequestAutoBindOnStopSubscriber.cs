using Microsoft.Extensions.Logging;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed partial class PullRequestAutoBindOnStopSubscriber(
    IIntentRepositoryBindingRepository bindings,
    PullRequestAutoBindWorkflow autoBind,
    ILogger<PullRequestAutoBindOnStopSubscriber> logger)
    : ITerminalHookSubscriber
{
    public async Task HandleAsync(TerminalHookEvent hook, CancellationToken ct)
    {
        if (hook.Event != TerminalHookEvents.Stop)
        {
            return;
        }

        var intentBindings = await bindings.FindByIntentAsync(new IntentId(hook.IntentId), ct);
        var candidates = intentBindings
            .Where(binding =>
                binding.State.CloneStatus == CloneStatusNames.Ready
                && binding.State.PullRequestNumber is null)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var binding in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var report = await autoBind.RunForAsync(binding, ct);
            LogAttempt(
                logger,
                hook.IntentId,
                binding.Id.Value,
                report.Bound,
                report.Skipped,
                report.Failed);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Terminal Stop PR auto-bind: intent={IntentId}, binding={BindingId}, bound={Bound}, skipped={Skipped}, failed={Failed}.")]
    private static partial void LogAttempt(
        ILogger logger,
        string intentId,
        string bindingId,
        int bound,
        int skipped,
        int failed);
}
