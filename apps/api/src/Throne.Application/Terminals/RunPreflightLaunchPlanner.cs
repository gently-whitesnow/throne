using Throne.Application.Ports;

namespace Throne.Application.Terminals;

public sealed class RunPreflightLaunchPlanner(
    TerminalLaunchResolver resolver,
    IIntentTerminalLaunchStore store)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptySelections =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public Task<RunPreflightLaunchPlan> ResolveAsync(
        string mode,
        TerminalLaunchInput launch,
        CancellationToken ct) =>
        ResolveAsync(mode, launch, intentId: null, ct);

    /// <summary>
    /// Resolves the launch axis and, when <paramref name="intentId"/> is provided, joins it with
    /// the previously persisted <c>attached_skill_ids</c> and per-mode skill selection so the
    /// response echoes the survived runtime + remembered sets (the run pipeline never overwrites
    /// them through <see cref="IIntentTerminalLaunchStore.SaveAsync"/>).
    /// </summary>
    public async Task<RunPreflightLaunchPlan> ResolveAsync(
        string mode,
        TerminalLaunchInput launch,
        string? intentId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var options = await resolver.ResolveAsync(launch.Vendor, launch.Model, launch.Effort, ct);
        IReadOnlyList<string> attached = Array.Empty<string>();
        IReadOnlyDictionary<string, IReadOnlyList<string>> selections = EmptySelections;
        if (!string.IsNullOrWhiteSpace(intentId))
        {
            var previous = await store.GetAsync(intentId, ct);
            attached = previous?.AttachedSkillIds ?? Array.Empty<string>();
            selections = previous?.SelectedSkillIdsByMode ?? EmptySelections;
        }
        var record = new TerminalLaunchRecord(
            mode, options.Vendor, options.Model, options.Effort, attached, selections);
        return new RunPreflightLaunchPlan(options, record);
    }

    public Task SaveAsync(string intentId, RunPreflightLaunchPlan plan, CancellationToken ct) =>
        store.SaveAsync(intentId, plan.Record, ct);
}

public sealed record RunPreflightLaunchPlan(
    TerminalLaunchOptions Options,
    TerminalLaunchRecord Record);
