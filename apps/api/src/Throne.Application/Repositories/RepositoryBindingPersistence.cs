using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Persistence-orchestration helper. Owns construction of fresh bindings
/// (workspace-path layout, factory invocation, clock) and the create/delete/save/find
/// roundtrips through the unit-of-work + Mongo port.
/// </summary>
public sealed class RepositoryBindingPersistence(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public IntentRepositoryBinding BuildPendingBinding(
        BindRepositoryCommand command,
        IntentId intentId,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(command);
        RepoCoordinate coordinate;
        try
        {
            coordinate = new RepoCoordinate(command.Provider, command.Owner, command.Repo);
        }
        catch (ArgumentException ex)
        {
            // Surface owner/repo allow-list / length / `..` traversal violations as a
            // 422 validation failure instead of letting the raw ArgumentException
            // bubble to ASP.NET's default 500 handler. ADR-0024 § 1 invariant.
            throw RepositoryBindingFailures.InvalidCoordinate(command.Owner, command.Repo, ex.Message);
        }
        var defaultBranch = string.IsNullOrWhiteSpace(command.DefaultBranch) ? "main" : command.DefaultBranch.Trim();
        var workspacePath = WorkspacePathLayout.Compute(workspaceRoot, intentId, coordinate);
        return IntentRepositoryBindingFactory.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: coordinate,
            defaultBranch: defaultBranch,
            workspacePath: workspacePath,
            pullRequestNumber: command.PullRequestNumber,
            now: clock.GetUtcNow());
    }

    public async Task<IntentRepositoryBinding> CreateAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        var outcome = await unitOfWork.ExecuteAsync(inner => bindings.CreateAsync(binding, inner), ct);
        return outcome switch
        {
            CreateBindingOutcome.Created c => c.Binding,
            CreateBindingOutcome.Duplicate dup => throw RepositoryBindingFailures.DuplicateBinding(dup.Existing),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }

    public async Task DeleteAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        var outcome = await unitOfWork.ExecuteAsync(inner => bindings.DeleteAsync(binding.Id, inner), ct);
        if (outcome is DeleteBindingOutcome.NotFound)
        {
            throw RepositoryBindingFailures.BindingNotFound(binding.IntentId.Value, binding.Id.Value);
        }
    }

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(IntentId intentId, CancellationToken ct) =>
        bindings.FindByIntentAsync(intentId, ct);
}
