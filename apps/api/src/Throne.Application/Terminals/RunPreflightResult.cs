using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

/// <summary>
/// Snapshot returned by <see cref="RunPreflightOrchestrator"/> after one pass through
/// the Slice 2 Run pipeline (auto-bind → clone-wait → tmux-spawn).
/// </summary>
/// <param name="IntentId">Intent the pre-flight ran for (echoed for the HTTP response).</param>
/// <param name="SessionName">Deterministic tmux session name <c>throne-{intent_id}</c>.</param>
/// <param name="SessionState">Lifecycle bucket — see <see cref="TerminalSessionStates"/>.</param>
/// <param name="Bindings">Every binding attached to the intent after the auto-bind step.</param>
/// <param name="BlockingBindings">
///   IDs of bindings whose <c>clone_status</c> ended up in <c>failed</c> or <c>broken</c>.
///   Populated when <paramref name="SessionState"/> = <see cref="TerminalSessionStates.Blocked"/>.
/// </param>
/// <param name="Launch">
///   Resolved launch axis surfaced to the UI (ADR-0041): the axis a run actually
///   spawned with, or the persisted last-used axis on the status probe. Null when the intent
///   was never launched.
/// </param>
public sealed record RunPreflightResult(
    string IntentId,
    string SessionName,
    string SessionState,
    IReadOnlyList<RunPreflightBindingStatus> Bindings,
    IReadOnlyList<string> BlockingBindings,
    TerminalLaunchRecord? Launch);

/// <summary>
/// Per-binding snapshot inside <see cref="RunPreflightResult"/>. Mirrors the wire
/// shape of <c>RunIntentBindingStatusDto</c> with domain types.
/// </summary>
public sealed record RunPreflightBindingStatus(
    string BindingId,
    string CloneStatus,
    string? CloneError);

/// <summary>
/// Closed enum of tmux-session lifecycle buckets surfaced by the HTTP <c>run</c> endpoint
/// and status probe. Mirrors the OpenAPI <c>TerminalSessionState</c> enum.
/// </summary>
public static class TerminalSessionStates
{
    public const string Spawning = "spawning";
    public const string Running = "running";
    public const string Blocked = "blocked";
    public const string Exited = "exited";
}

internal static class RunPreflightBindingMapper
{
    public static RunPreflightBindingStatus ToStatus(IntentRepositoryBinding binding) =>
        new(
            BindingId: binding.Id.Value,
            CloneStatus: binding.State.CloneStatus,
            CloneError: binding.State.CloneError);
}
