using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Events;
using Throne.Domain.Tags;

namespace Throne.Application.Terminals;

/// <summary>
/// Detaches the post-spawn prompt delivery from the Run pre-flight's critical path. Behind a seam so
/// the spawn orchestration can be unit-tested for "delivery kicked" without racing the background
/// task, and the delivery mechanics are covered directly via <see cref="RunPreflightPromptDelivery"/>.
/// </summary>
public interface IRunPreflightPromptDelivery
{
    void Kick(TerminalPromptDeliveryRequest request);
}

/// <summary>
/// Post-spawn user-prompt delivery, run as a detached best-effort task so the Run pre-flight returns
/// <c>Running</c> the instant tmux reports the session alive (the live pane is the honest progress
/// indicator). Delivery — TUI readiness wait → bracketed-paste → submit confirmation — used to run
/// synchronously inside <see cref="RunPreflightSpawn"/> and throw on timeout, which surfaced as a
/// scary 500 even though the session was alive and the prompt was usually delivered. Here it is soft:
/// the session is already <c>Running</c>; a readiness/submit timeout dispatches
/// <see cref="TerminalPromptSubmitUnconfirmed"/> so the front shows an inline nudge, never an error.
/// </summary>
public sealed partial class RunPreflightPromptDelivery(
    ITmuxSessionManager tmux,
    TmuxTuiReadinessWaiter readinessWaiter,
    TmuxPromptSubmitConfirmer confirmer,
    TerminalPromptSubmitSignals submitSignals,
    IDomainEventDispatcher events,
    TerminalDeliveryMapContextReader mapContext,
    ILogger<RunPreflightPromptDelivery>? log = null) : IRunPreflightPromptDelivery
{
    private const string UserPromptFileName = "throne-session.user-prompt.txt";
    private readonly ILogger<RunPreflightPromptDelivery> _log = log ?? NullLogger<RunPreflightPromptDelivery>.Instance;

    /// <summary>
    /// Schedules delivery on the thread pool and returns immediately. Detached on purpose: the HTTP
    /// request that spawned the session is already returning <c>Running</c>, so delivery must outlive
    /// it (and must not ride the request's cancellation token). Failures are swallowed into a logged
    /// soft event — a half-delivered prompt is a UX nudge, not a request error.
    /// </summary>
    public void Kick(TerminalPromptDeliveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = Task.Run(async () =>
        {
            try
            {
                await DeliverAsync(request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogDeliveryFaulted(_log, ex, request.IntentId, request.Vendor);
                await TryDispatchUnconfirmedAsync(request.IntentId);
            }
        });
    }

    /// <summary>
    /// The awaitable delivery body. <see cref="Kick"/> fires this detached; tests await it directly.
    /// </summary>
    public async Task DeliverAsync(TerminalPromptDeliveryRequest request, CancellationToken ct)
    {
        Directory.CreateDirectory(request.WorkspacePath);
        var promptPath = Path.Combine(request.WorkspacePath, UserPromptFileName);
        // Prepend the workspace map so the agent reads the real repo paths instead of guessing the
        // clone sub-dir name. Pasted verbatim — confirmation matches against the composed body. Tag
        // names, links and attachments are resolved here by intent id, off the pre-flight critical
        // path, since this task is detached; the curated session skills ride on the request.
        var ctx = await mapContext.ReadAsync(request.IntentId, request.TagIds, ct);
        var composedPrompt = WorkspaceMapPrompt.Compose(
            request.WorkspacePath, request.RepoPaths, ctx.Tags, ctx.Links,
            ctx.Attachments, ctx.CardAttachments, request.SessionSkillIds, request.UserPrompt);
        await File.WriteAllTextAsync(promptPath, composedPrompt, ct);
        LogDeliveryPrepared(_log, request.IntentId, request.Mode, request.Vendor, composedPrompt.Length, promptPath);

        // Unknown vendors have no adapter (no readiness glyph, no submit hook) — paste best-effort and
        // leave confirmation to the operator watching the live pane.
        if (request.Adapter is null)
        {
            await tmux.PasteFileAsSubmittedPromptAsync(request.IntentId, promptPath, ct);
            return;
        }

        using var readiness = readinessWaiter.Arm(request.IntentId, request.Adapter);
        var readinessResult = await readinessWaiter.WaitAsync(request.IntentId, request.Adapter, readiness, ct);
        LogReadinessFinished(_log, request.IntentId, request.Vendor, readinessResult.IsReady, readinessResult.Attempts);
        if (!readinessResult.IsReady)
        {
            await DispatchUnconfirmedAsync(request.IntentId, "tui_not_ready", ct);
            return;
        }

        // Arm before the paste's trailing Enter so the authoritative UserPromptSubmit hook is not lost.
        using var submitSignal = submitSignals.Arm(request.IntentId);
        await tmux.PasteFileAsSubmittedPromptAsync(request.IntentId, promptPath, ct);
        LogSubmitCommandReturned(_log, request.IntentId, request.Mode, request.Vendor, promptPath);

        var confirmation = await confirmer.ConfirmAsync(
            request.IntentId, request.Adapter, composedPrompt, submitSignal, ct);
        if (!confirmation.IsConfirmed)
        {
            await DispatchUnconfirmedAsync(request.IntentId, "submit_not_acknowledged", ct);
        }
    }

    private async Task DispatchUnconfirmedAsync(string intentId, string reason, CancellationToken ct)
    {
        LogUnconfirmed(_log, intentId, reason);
        await events.DispatchAsync(new TerminalPromptSubmitUnconfirmed(intentId), ct);
    }

    private async Task TryDispatchUnconfirmedAsync(string intentId)
    {
        try
        {
            await events.DispatchAsync(new TerminalPromptSubmitUnconfirmed(intentId), CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogDeliveryFaulted(_log, ex, intentId, "unknown");
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "terminal background prompt delivery prepared: intent={IntentId} mode={Mode} vendor={Vendor} chars={CharCount} file={PromptPath}")]
    private static partial void LogDeliveryPrepared(
        ILogger logger, string intentId, string mode, string vendor, int charCount, string promptPath);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "terminal background prompt readiness gate finished: intent={IntentId} vendor={Vendor} ready={Ready} attempts={Attempts}")]
    private static partial void LogReadinessFinished(
        ILogger logger, string intentId, string vendor, bool ready, int attempts);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "terminal background prompt submit command returned: intent={IntentId} mode={Mode} vendor={Vendor} file={PromptPath}")]
    private static partial void LogSubmitCommandReturned(
        ILogger logger, string intentId, string mode, string vendor, string promptPath);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "terminal background prompt delivery unconfirmed: intent={IntentId} reason={Reason} — session is alive, surfacing soft hint.")]
    private static partial void LogUnconfirmed(ILogger logger, string intentId, string reason);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error,
        Message = "terminal background prompt delivery faulted: intent={IntentId} vendor={Vendor}.")]
    private static partial void LogDeliveryFaulted(ILogger logger, Exception ex, string intentId, string vendor);
}

/// <summary>
/// Inputs for a detached post-spawn prompt delivery. <see cref="Adapter"/> is null for unknown
/// vendors (best-effort paste, no readiness/confirm gates). <see cref="RepoPaths"/> are the absolute
/// clone paths of the intent's ready repos and <see cref="TagIds"/> its tags (resolved to names at
/// delivery), rendered as a workspace map atop the pasted prompt. Link micro-facts and attachments
/// are resolved by intent id at delivery time, matching the preflight preview filter.
/// <see cref="SessionSkillIds"/> is the curated per-mode skill set from the planner, listed in the
/// map so the agent sees which skills the operator loaded for this session.
/// </summary>
public sealed record TerminalPromptDeliveryRequest(
    string IntentId,
    string Mode,
    string Vendor,
    ISessionHookAdapter? Adapter,
    string WorkspacePath,
    IReadOnlyList<string> RepoPaths,
    IReadOnlyList<TagId> TagIds,
    IReadOnlyList<string> SessionSkillIds,
    string UserPrompt);
