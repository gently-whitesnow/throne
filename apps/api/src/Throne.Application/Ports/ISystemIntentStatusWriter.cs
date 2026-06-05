using Throne.Domain.Intents;

namespace Throne.Application.Ports;

/// <summary>
/// Owner-agnostic intent read/write surface for system-initiated hooks that run outside an
/// HTTP request — there is no <c>ICurrentUserAccessor</c> context, so the owner-scoped
/// <see cref="IIntentRepository"/> path cannot be used. Currently backs the PR-merge
/// auto-close (<c>IntentMergeAutoCloser</c>); kept as a separate narrow port so the broad
/// owner-scoped repository stays unchanged.
/// </summary>
public interface ISystemIntentStatusWriter
{
    /// <summary>Resolve an intent by id alone (no owner filter).</summary>
    Task<Intent?> GetByIdForSystemAsync(IntentId id, CancellationToken ct);

    /// <summary>
    /// Transition status as <see cref="Throne.Domain.Intents.Training.IntentTrainingAuthor.System"/>,
    /// recording the change with <paramref name="source"/> / <paramref name="reason"/>. Resolves the
    /// intent by id alone. Returns <c>Updated</c> (carrying <c>IntentStatusChanged</c>) on a real
    /// transition; the caller owns the «only from a non-terminal status» guard.
    /// </summary>
    Task<SetIntentStatusOutcome> SetStatusBySystemAsync(
        IntentId id,
        string status,
        string? reason,
        string source,
        DateTimeOffset now,
        CancellationToken ct);
}
