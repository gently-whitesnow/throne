using System.Globalization;
using Microsoft.Extensions.Logging;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Closes an intent when its pull request(s) merge. Triggered from the polling tick the
/// moment a binding flips to <c>pull_request_state = merged</c>
/// (<see cref="PullRequestSyncBindingVisitor"/>).
///
/// Close condition (intent spec Q6): the intent is closed only when <b>all</b> of its
/// PR-bearing bindings are <c>merged</c>; bindings without an attached PR never block, and
/// an intent with no PR-bearing binding has no trigger at all. The status move targets
/// <c>done</c> from any non-terminal status (Q3) and is idempotent — a binding observed as
/// merged twice, or an already-terminal intent, is a no-op.
///
/// This is a product-level system hook, not an agent initiative, so it bypasses the
/// «status changes only on explicit operator request» rule. It runs in a background scope
/// with no HTTP user, hence the owner-agnostic <see cref="IIntentRepository.GetByIdForSystemAsync"/>
/// / <see cref="IIntentRepository.SetStatusBySystemAsync"/> path. The resulting
/// <c>IntentStatusChanged</c> event drives realtime fan-out and the tmux teardown
/// (<c>TerminalKillOnIntentDoneHandler</c>, ADR-0026 § 8).
/// </summary>
public sealed partial class IntentMergeAutoCloser(
    IIntentRepositoryBindingRepository bindings,
    ISystemIntentStatusWriter intents,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<IntentMergeAutoCloser> logger)
{
    public const string Source = "pr_merge";

    public async Task OnBindingMergedAsync(IntentRepositoryBinding mergedBinding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mergedBinding);

        var siblings = await bindings.FindByIntentAsync(mergedBinding.IntentId, ct);
        var prBearing = siblings.Where(b => b.State.PullRequestNumber is not null).ToList();
        var mergedCount = prBearing.Count(b => b.State.PullRequestState == PullRequestStateNames.Merged);
        if (prBearing.Count == 0 || mergedCount != prBearing.Count)
        {
            LogSkipNotAllMerged(
                logger, mergedBinding.IntentId.Value, mergedBinding.Id.Value, mergedCount, prBearing.Count);
            return;
        }

        var intent = await intents.GetByIdForSystemAsync(mergedBinding.IntentId, ct);
        if (intent is null || IntentStatusNames.IsTerminal(intent.State.Status))
        {
            LogSkipIntent(logger, mergedBinding.IntentId.Value, intent?.State.Status ?? "<null>");
            return;
        }

        var pr = mergedBinding.State.PullRequestNumber!.Value;
        var reason = $"Закрыт автоматически по мерджу PR #{pr.ToString(CultureInfo.InvariantCulture)}.";
        var fromStatus = intent.State.Status;
        var outcome = await unitOfWork.ExecuteAsync(
            inner => intents.SetStatusBySystemAsync(
                mergedBinding.IntentId, IntentStatusNames.Done, reason, Source, clock.GetUtcNow(), inner),
            ct);
        var intentId = mergedBinding.IntentId.Value;
        switch (outcome)
        {
            case SetIntentStatusOutcome.Updated:
                LogClosed(logger, intentId, pr, fromStatus);
                break;
            case SetIntentStatusOutcome.Conflict conflict:
                LogCloseConflict(logger, intentId, pr, conflict.CurrentVersion, conflict.CurrentStatus);
                break;
            case SetIntentStatusOutcome.NotFound:
                LogCloseNotFound(logger, intentId, pr);
                break;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "IntentMergeAutoCloser: not all PR-bearing bindings merged for intent {IntentId} "
            + "(trigger binding {BindingId}): {Merged}/{Total} merged — intent left open.")]
    private static partial void LogSkipNotAllMerged(
        ILogger logger, string intentId, string bindingId, int merged, int total);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "IntentMergeAutoCloser: intent {IntentId} not closable (status={Status}) — skip.")]
    private static partial void LogSkipIntent(ILogger logger, string intentId, string status);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "IntentMergeAutoCloser: intent {IntentId} closed on PR #{Pr} ({FromStatus} -> done).")]
    private static partial void LogClosed(ILogger logger, string intentId, int pr, string fromStatus);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "IntentMergeAutoCloser: intent {IntentId} close on PR #{Pr} hit version conflict "
            + "(current version={CurrentVersion}, status={CurrentStatus}) — intent left open.")]
    private static partial void LogCloseConflict(
        ILogger logger, string intentId, int pr, int currentVersion, string currentStatus);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "IntentMergeAutoCloser: intent {IntentId} close on PR #{Pr} found no such intent — skip.")]
    private static partial void LogCloseNotFound(ILogger logger, string intentId, int pr);
}
