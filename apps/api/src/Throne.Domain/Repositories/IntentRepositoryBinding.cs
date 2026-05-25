using Throne.Domain.Intents;

namespace Throne.Domain.Repositories;

/// <summary>
/// First-class aggregate (see ADR-0024) — NOT serialised into <c>Intent.text</c>
/// and NOT modelled as an <c>intent_links</c> edge (see ADR-0018).
///
/// Invariants:
/// <list type="bullet">
///   <item>The tuple <c>(IntentId, Provider, Owner, Repo)</c> is unique (enforced by repository).</item>
///   <item><see cref="WorkspacePath"/> is immutable after creation.</item>
///   <item>Status machine: <c>pending → cloning → ready | failed</c>; <c>ready → broken</c>
///         only via polling-observed 404, not from the bind flow.</item>
///   <item><see cref="AttachPullRequest"/> is valid only when no PR is attached yet.</item>
/// </list>
/// </summary>
public sealed class IntentRepositoryBinding
{
    internal IntentRepositoryBinding(
        BindingId id,
        IntentId intentId,
        RepoCoordinate coordinate,
        string workspacePath,
        DateTimeOffset createdAt,
        IntentRepositoryBindingState state)
    {
        Id = id;
        IntentId = intentId;
        Coordinate = coordinate;
        WorkspacePath = workspacePath;
        CreatedAt = createdAt;
        State = state;
    }

    public BindingId Id { get; }
    public IntentId IntentId { get; }
    public RepoCoordinate Coordinate { get; }
    public string WorkspacePath { get; }
    public DateTimeOffset CreatedAt { get; }
    public IntentRepositoryBindingState State { get; internal set; }
}
