using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Application.Terminals;

/// <summary>
/// Stateless «which repos should be bound to this intent by the time we spawn the
/// agent» pass. Source of truth: <see cref="Tag.DefaultRepositories"/> unioned across
/// every tag attached to the intent. The aggregate is computed on every Run — no
/// <c>auto_bound_at</c> marker on the intent (Slice 2 «Решения интервью» KISS).
///
/// Diff and bind only; clone-wait + tmux-spawn live in the orchestrator above.
/// </summary>
public sealed class RunPreflightAutoBind(
    TagDefaultsUnion union,
    IIntentRepositoryBindingRepository bindings,
    RepositoryBindingService bindingService)
{
    public async Task ApplyAsync(Intent intent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var defaults = await union.ComputeAsync(intent, ct);
        if (defaults.Count == 0)
        {
            return;
        }

        var existingKeys = await LoadExistingKeysAsync(intent.Id, ct);
        foreach (var defaultRepo in defaults)
        {
            if (existingKeys.Contains(TagDefaultsUnion.CoordinateKey(defaultRepo.Coordinate)))
            {
                continue;
            }
            _ = await BindAsync(intent.Id.Value, defaultRepo, ct);
        }
    }

    private async Task<HashSet<string>> LoadExistingKeysAsync(IntentId intentId, CancellationToken ct)
    {
        var existing = await bindings.FindByIntentAsync(intentId, ct);
        return existing
            .Select(b => TagDefaultsUnion.CoordinateKey(b.Coordinate))
            .ToHashSet(StringComparer.Ordinal);
    }

    private Task<IntentRepositoryBinding> BindAsync(
        string intentId,
        TagDefaultRepository defaultRepo,
        CancellationToken ct) =>
        // enqueueClone: false — RunPreflightCloneScheduler runs in the next orchestrator step
        // and enqueues every pending binding, so enqueuing here too would queue this binding
        // twice for a single Run (and race two `gh clone` into the same workspace path).
        bindingService.BindAsync(
            new BindRepositoryCommand(
                IntentId: intentId,
                Provider: defaultRepo.Coordinate.Provider,
                Owner: defaultRepo.Coordinate.Owner,
                Repo: defaultRepo.Coordinate.Repo,
                DefaultBranch: defaultRepo.DefaultBranch,
                PullRequestNumber: null),
            ct,
            enqueueClone: false);
}
