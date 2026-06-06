using System.Globalization;
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
public sealed class IntentMergeAutoCloser(
    IIntentRepositoryBindingRepository bindings,
    ISystemIntentStatusWriter intents,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string Source = "pr_merge";

    public async Task OnBindingMergedAsync(IntentRepositoryBinding mergedBinding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mergedBinding);

        var siblings = await bindings.FindByIntentAsync(mergedBinding.IntentId, ct);
        var prBearing = siblings.Where(b => b.State.PullRequestNumber is not null).ToList();
        if (prBearing.Count == 0 ||
            !prBearing.All(b => b.State.PullRequestState == PullRequestStateNames.Merged))
        {
            return;
        }

        var intent = await intents.GetByIdForSystemAsync(mergedBinding.IntentId, ct);
        if (intent is null || IntentStatusNames.IsTerminal(intent.State.Status))
        {
            return;
        }

        var pr = mergedBinding.State.PullRequestNumber!.Value.ToString(CultureInfo.InvariantCulture);
        var reason = $"Закрыт автоматически по мерджу PR #{pr}.";
        await unitOfWork.ExecuteAsync(
            inner => intents.SetStatusBySystemAsync(
                mergedBinding.IntentId, IntentStatusNames.Done, reason, Source, clock.GetUtcNow(), inner),
            ct);
    }
}
