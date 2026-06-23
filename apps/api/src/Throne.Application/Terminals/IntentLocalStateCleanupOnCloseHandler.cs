using Microsoft.Extensions.Logging;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Wipes an intent's local state when it is closed — <c>done</c> or <c>reject</c>: the per-directory
/// agent trust entries (claude + codex) and the whole workspace folder. Fires for the PR-merge
/// auto-close, the operator's manual «закрыть как готово», and a manual reject — all land on the
/// same <see cref="IntentStatusChanged"/> event, sibling to <c>TerminalKillOnIntentCloseHandler</c>.
/// <c>fridge</c> is a pause, not a close, so its state survives.
///
/// Gated solely by <see cref="IntentState.CleanupLocalStateOnClose"/> (default true). The
/// merge-control checkbox edits that flag: merging with it cleared persists <c>false</c> and
/// suppresses auto-close, so the intent stays open and any later manual close respects the
/// earlier choice and keeps the state.
///
/// Best-effort: a trust or directory failure is swallowed and logged so it never aborts the
/// post-commit event fan-out nor rolls back the status change / session teardown. The intent
/// folder is resolved from the workspace root (not a saved binding path) so the sweep captures
/// leftover or orphan clones, not just the current bindings.
/// </summary>
public sealed partial class IntentLocalStateCleanupOnCloseHandler(
    IWorkspaceTrust trust,
    IWorkspaceDirectoryRemover directories,
    IWorkspaceRootProvider workspaceRoot,
    IEnumerable<ISessionHookAdapter> spawnAdapters,
    ILogger<IntentLocalStateCleanupOnCloseHandler> logger) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        if (evt is not IntentStatusChanged changed || !IntentStatusNames.IsClosing(changed.Intent.State.Status))
        {
            return;
        }

        var intentId = changed.Intent.Id.Value;
        if (!changed.Intent.State.CleanupLocalStateOnClose)
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

        await CleanupSpawnAdaptersAsync(intentId, ct);

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

    // Best-effort fan-out: each vendor adapter reaps its own out-of-workspace per-session files
    // (Codex's $CODEX_HOME profile; Claude's live in the workspace folder removed below). Kept in its
    // own method so HandleAsync stays a thin sequencer within the type-level complexity budget.
    private async Task CleanupSpawnAdaptersAsync(string intentId, CancellationToken ct)
    {
        foreach (var adapter in spawnAdapters)
        {
            try
            {
                await adapter.CleanupAsync(intentId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogSpawnCleanupFailed(logger, intentId, adapter.Vendor, ex);
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "IntentLocalStateCleanupOnClose: invoked for intent {IntentId} (dir={IntentDir}).")]
    private static partial void LogInvoked(ILogger logger, string intentId, string intentDir);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "IntentLocalStateCleanupOnClose: intent {IntentId} cleanup skipped — gate off "
            + "(cleanup_local_state_on_done=false).")]
    private static partial void LogSkipDisabled(ILogger logger, string intentId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "IntentLocalStateCleanupOnClose: removed workspace folder {IntentDir} for intent {IntentId}.")]
    private static partial void LogDirectoryRemoved(ILogger logger, string intentId, string intentDir);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "IntentLocalStateCleanupOnClose: trust cleanup threw for intent {IntentId} — "
            + "swallowed (best-effort), stale trust entries may remain.")]
    private static partial void LogTrustFailed(ILogger logger, string intentId, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "IntentLocalStateCleanupOnClose: removing workspace folder {IntentDir} threw for "
            + "intent {IntentId} — swallowed (best-effort), the folder may remain on disk.")]
    private static partial void LogDirectoryFailed(ILogger logger, string intentId, string intentDir, Exception ex);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning,
        Message = "IntentLocalStateCleanupOnClose: {Vendor} spawn-adapter cleanup threw for intent "
            + "{IntentId} — swallowed (best-effort), a per-session file may remain.")]
    private static partial void LogSpawnCleanupFailed(ILogger logger, string intentId, string vendor, Exception ex);
}
