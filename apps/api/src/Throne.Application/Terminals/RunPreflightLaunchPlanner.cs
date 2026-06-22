using Throne.Application.Ports;

namespace Throne.Application.Terminals;

public sealed class RunPreflightLaunchPlanner(
    TerminalLaunchResolver resolver,
    IIntentTerminalLaunchStore store)
{
    public Task<RunPreflightLaunchPlan> ResolveAsync(
        string mode,
        TerminalLaunchInput launch,
        CancellationToken ct) =>
        ResolveAsync(mode, launch, intentId: null, ct);

    /// <summary>
    /// Resolves the launch axis and, when <paramref name="intentId"/> is provided, joins it with
    /// the previously persisted <c>attached_skill_ids</c> so the response echoes the survived
    /// hot-attached set (the run pipeline never overwrites them — see ADR-0041 follow-up
    /// for hot-attach).
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
        if (!string.IsNullOrWhiteSpace(intentId))
        {
            var previous = await store.GetAsync(intentId, ct);
            attached = previous?.AttachedSkillIds ?? Array.Empty<string>();
        }
        var record = new TerminalLaunchRecord(mode, options.Vendor, options.Model, options.Effort, attached);
        return new RunPreflightLaunchPlan(options, record);
    }

    public Task SaveAsync(string intentId, RunPreflightLaunchPlan plan, CancellationToken ct) =>
        store.SaveAsync(intentId, plan.Record, ct);
}

public sealed record RunPreflightLaunchPlan(
    TerminalLaunchOptions Options,
    TerminalLaunchRecord Record);
