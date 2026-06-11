using Microsoft.Extensions.Logging;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Wipes an intent's local state when it reaches <c>done</c>: the per-directory agent trust
/// entries (claude + codex) and the whole workspace folder. Fires for both the PR-merge
/// auto-close and the operator's manual «закрыть как готово» — both land on the same
/// <see cref="IntentStatusChanged"/> event, sibling to <c>TerminalKillOnIntentDoneHandler</c>.
///
/// Gated solely by <see cref="IntentState.CleanupLocalStateOnDone"/> (default true). The
/// merge-control checkbox edits that flag: merging with it cleared persists <c>false</c> and
/// suppresses auto-close, so the intent stays open and any later manual <c>done</c> respects the
/// earlier choice and keeps the state.
///
/// Best-effort: a trust or directory failure is swallowed and logged so it never aborts the
/// post-commit event fan-out nor rolls back the status change / session teardown. The intent
/// folder is resolved from the workspace root (not a saved binding path) so the sweep captures
/// leftover or orphan clones, not just the current bindings.
/// </summary>
public sealed partial class IntentLocalStateCleanupOnDoneHandler(
    IWorkspaceTrust trust,
    IWorkspaceDirectoryRemover directories,
    IWorkspaceRootProvider workspaceRoot,
    ILogger<IntentLocalStateCleanupOnDoneHandler> logger) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        if (evt is not IntentStatusChanged changed ||
            !string.Equals(changed.Intent.State.Status, IntentStatusNames.Done, StringComparison.Ordinal))
        {
            return;
        }

        var intentId = changed.Intent.Id.Value;
        if (!changed.Intent.State.CleanupLocalStateOnDone)
        {
            LogSkipDisabled(logger, intentId);
            return;
        }

        var intentDir = WorkspacePathLayout.ComputeIntentRoot(workspaceRoot.ResolvedRoot, changed.Intent.Id);
        LogInvoked(logger, intentId, intentDir);

        try
        {
            await trust.RemoveTrustedUnderAsync(intentDir, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogTrustFailed(logger, intentId, ex);
        }

        try
        {
            await directories.RemoveAsync(intentDir, ct);
            LogDirectoryRemoved(logger, intentId, intentDir);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogDirectoryFailed(logger, intentId, intentDir, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "IntentLocalStateCleanupOnDone: invoked for intent {IntentId} (dir={IntentDir}).")]
    private static partial void LogInvoked(ILogger logger, string intentId, string intentDir);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "IntentLocalStateCleanupOnDone: intent {IntentId} cleanup skipped — gate off "
            + "(cleanup_local_state_on_done=false).")]
    private static partial void LogSkipDisabled(ILogger logger, string intentId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "IntentLocalStateCleanupOnDone: removed workspace folder {IntentDir} for intent {IntentId}.")]
    private static partial void LogDirectoryRemoved(ILogger logger, string intentId, string intentDir);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "IntentLocalStateCleanupOnDone: trust cleanup threw for intent {IntentId} — "
            + "swallowed (best-effort), stale trust entries may remain.")]
    private static partial void LogTrustFailed(ILogger logger, string intentId, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "IntentLocalStateCleanupOnDone: removing workspace folder {IntentDir} threw for "
            + "intent {IntentId} — swallowed (best-effort), the folder may remain on disk.")]
    private static partial void LogDirectoryFailed(ILogger logger, string intentId, string intentDir, Exception ex);
}
