using Throne.Api.Repositories;
using Throne.Application.Events;
using Throne.Domain.Repositories;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

/// <summary>
/// Maps Intent ↔ repository binding lifecycle domain events (ADR-0024)
/// onto the contract-first realtime envelopes declared in
/// <c>specs/contracts/realtime/events.yaml</c>:
/// <list type="bullet">
///   <item><c>intent.repository_bound</c> — full <see cref="Throne.Repositories.Contracts.Generated.RepositoryBindingDto"/>;</item>
///   <item><c>intent.repository_unbound</c> — <c>{ intent_id, binding_id }</c>;</item>
///   <item><c>intent.repository_clone_progress</c> — <c>{ intent_id, binding_id, status, error? }</c>;</item>
///   <item><c>intent.pr_comment_added</c> — <c>{ intent_id, binding_id, comment }</c>.</item>
/// </list>
/// Subscription scope is per-<c>intent_id</c>; matches existing <c>intent.*</c> behaviour
/// and is indistinguishable from webhook fan-out on the frontend side.
/// </summary>
internal static class IntentRepositoryRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        IntentRepositoryBound bound => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentRepositoryBound,
            RepositoryBindingDtoMapper.ToDto(bound.Binding)),
        IntentRepositoryUnbound unbound => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentRepositoryUnbound,
            new
            {
                intent_id = unbound.Binding.IntentId.Value,
                binding_id = unbound.Binding.Id.Value,
            }),
        IntentRepositoryCloneProgress progress => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentRepositoryCloneProgress,
            BuildCloneProgressPayload(progress.Binding)),
        IntentPrCommentAdded commentAdded => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentPrCommentAdded,
            new
            {
                intent_id = commentAdded.Binding.IntentId.Value,
                binding_id = commentAdded.Binding.Id.Value,
                comment = PullRequestCommentDtoMapper.ToDto(commentAdded.Comment, commentAdded.Binding.Id),
            }),
        _ => null,
    };

    private static object BuildCloneProgressPayload(IntentRepositoryBinding binding)
    {
        // <see cref="IntentRepositoryBindingState.CloneStatus"/> already holds the wire
        // string (see <see cref="CloneStatusNames"/>) — emitting it as-is avoids the
        // generated <c>CloneStatus</c> enum (its <c>JsonStringEnumConverter</c> is
        // attached per-property, not at the type level, so it would serialise as int
        // inside an anonymous payload object).
        var status = binding.State.CloneStatus;
        var error = binding.State.CloneError;
        return error is null
            ? new
            {
                intent_id = binding.IntentId.Value,
                binding_id = binding.Id.Value,
                status,
            }
            : new
            {
                intent_id = binding.IntentId.Value,
                binding_id = binding.Id.Value,
                status,
                error,
            };
    }
}
