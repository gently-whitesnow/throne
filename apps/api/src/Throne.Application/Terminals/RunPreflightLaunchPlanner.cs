using Throne.Application.Ports;

namespace Throne.Application.Terminals;

public sealed class RunPreflightLaunchPlanner(
    TerminalLaunchResolver resolver,
    IIntentTerminalLaunchStore store)
{
    public async Task<RunPreflightLaunchPlan> ResolveAsync(
        string mode,
        TerminalLaunchInput launch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var options = await resolver.ResolveAsync(launch.Vendor, launch.Model, launch.Effort, ct);
        var record = new TerminalLaunchRecord(mode, options.Vendor, options.Model, options.Effort);
        return new RunPreflightLaunchPlan(options, record);
    }

    public Task SaveAsync(string intentId, RunPreflightLaunchPlan plan, CancellationToken ct) =>
        store.SaveAsync(intentId, plan.Record, ct);
}

public sealed record RunPreflightLaunchPlan(
    TerminalLaunchOptions Options,
    TerminalLaunchRecord Record);
